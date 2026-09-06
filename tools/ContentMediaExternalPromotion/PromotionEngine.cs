using dorks_and_dice_site.Services.Content.Storage;
using System.Data.Common;
using dorks_and_dice_site.Services.Content;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContentMediaExternalPromotion;

public sealed class PromotionEngine(Database local, Database backup, Database external,
    string webRoot, string stateDirectory, IReadOnlyList<ManifestEntry> manifest)
{
    private string PlanFile => Path.Combine(stateDirectory, "plan.json");
    private string JournalFile => Path.Combine(stateDirectory, "journal.json");
    internal Action<string>? Fault { get; set; } // Failure-injection seam, usable only by the disposable test project.
    public async Task<PromotionPlan> Plan()
    {
        var (source, old) = await ValidateLocal();
        var target = await external.Read();
        var plan = new PromotionPlan { DatabaseIdentity = external.Identity, ManifestHash = StateStore.Hash(manifest), LocalFingerprint = source.Fingerprint, BackupFingerprint = old.Fingerprint };
        foreach (var entry in manifest)
        {
            var asset = source.Asset(entry.LocalAssetKey)!;
            var item = new PlanEntry { Manifest = entry, Sha256 = asset.Text("asset_sha256"), FileName = asset.Text("asset_file_name"), MediaType = asset.Text("asset_media_type") };
            var page = target.Page(entry.Slug);
            if (page == null || page["page_current_revision_id"] == null) { item.MissingPage = true; item.Reason = "Missing External target; no page, media, or dependency will be created for this entry."; }
            else
            {
                var revision = target.Current(page);
                item.BodyReference = References.Contains(revision.Text("revision_body"), entry.OldUrl);
                item.MetadataReference = References.InMetadata(revision.Text("revision_metadata_json"), entry.OldUrl);
                var same = target.Rows("content_asset").Where(a => a["asset_sha256"] == item.Sha256).ToList();
                foreach (var candidate in same)
                    if (StateStore.Sha(await external.Bytes(candidate.Text("asset_key"))) != item.Sha256) throw new PromotionException("External media checksum is invalid.");
                var existing = same.FirstOrDefault(a => References.Contains(revision.Text("revision_body"), StateStore.Url(a)) || References.InMetadata(revision.Text("revision_metadata_json"), StateStore.Url(a))) ?? same.FirstOrDefault();
                if (existing != null)
                {
                    item.ExistingExternalKey = existing.Text("asset_key"); item.ProposedExternalUrl = StateStore.Url(existing);
                    item.ExistingDependency = target.Attached(page, item.ExistingExternalKey);
                }
                item.Applicable = item.BodyReference || item.MetadataReference || (existing != null &&
                    (References.Contains(revision.Text("revision_body"), StateStore.Url(existing)) || References.InMetadata(revision.Text("revision_metadata_json"), StateStore.Url(existing))));
                item.Safe = item.Applicable;
                item.Reason = item.Applicable ? "Use current External revision; replace manifest paths only." : "External no longer references this media; leave unchanged.";
                if (item.Applicable) plan.Pages.TryAdd(entry.Slug, target.Baseline(page));
            }
            plan.Entries.Add(item);
        }
        Directory.CreateDirectory(stateDirectory);
        if (File.Exists(PlanFile) || File.Exists(JournalFile)) throw new PromotionException("State already exists. Use a new ignored state directory to create a new plan.");
        StateStore.Save(PlanFile, plan);
        return plan;
    }
    private async Task<(Snapshot Local, Snapshot Backup)> ValidateLocal()
    {
        if (!local.IsSqlite || !backup.IsSqlite || local.Identity == external.Identity || backup.Identity == external.Identity || local.Identity == backup.Identity)
            throw new PromotionException("Source database identity check failed.");
        await local.Integrity(); await backup.Integrity();
        var source = await local.Read(); var old = await backup.Read();
        if (manifest.Count == 0 || manifest.Select(m => (m.Slug, m.OldUrl)).Distinct().Count() != manifest.Count)
            throw new PromotionException("Empty or duplicate manifest entries.");
        foreach (var entry in manifest)
        {
            if (!entry.OldUrl.StartsWith("/site-modes/", StringComparison.Ordinal) || !Guid.TryParseExact(entry.LocalAssetKey, "N", out _)) throw new PromotionException("Invalid explicit manifest entry.");
            var asset = source.Asset(entry.LocalAssetKey) ?? throw new PromotionException($"Local asset missing for {entry.Slug}.");
            var bytes = await local.Bytes(entry.LocalAssetKey);
            if (StateStore.Sha(bytes) != asset.Text("asset_sha256") || bytes.LongLength != asset.Number("asset_length")) throw new PromotionException($"Local media checksum failed for {entry.Slug}.");
            var page = source.Page(entry.Slug) ?? throw new PromotionException($"Local page missing: {entry.Slug}.");
            if (!source.Attached(page, entry.LocalAssetKey)) throw new PromotionException($"Local dependency missing: {entry.Slug}.");
            var revision = source.Current(page); var url = StateStore.Url(asset);
            if ((!References.Contains(revision.Text("revision_body"), url) && !References.InMetadata(revision.Text("revision_metadata_json"), url))
                || !source.Rows("content_revision_asset").Any(r => r["revision_id"] == revision["revision_id"] && r["asset_key"] == entry.LocalAssetKey))
                throw new PromotionException($"Local current revision does not use the manifest asset: {entry.Slug}.");
            var path = Path.GetFullPath(Path.Combine(webRoot, Uri.UnescapeDataString(entry.OldUrl).TrimStart('/')));
            if (!path.StartsWith(Path.GetFullPath(webRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new PromotionException("Static path escaped web root.");
            if (File.Exists(path) && StateStore.Sha(await File.ReadAllBytesAsync(path)) != asset.Text("asset_sha256")) throw new PromotionException($"Static media differs from Local: {entry.Slug}.");
        }
        return (source, old);
    }
    private async Task<PromotionPlan> LoadPlan()
    {
        var plan = StateStore.Load<PromotionPlan>(PlanFile);
        var (source, old) = await ValidateLocal();
        if (plan.Version != 1 || plan.DatabaseIdentity != external.Identity || plan.ManifestHash != StateStore.Hash(manifest)
            || source.Fingerprint != plan.LocalFingerprint || old.Fingerprint != plan.BackupFingerprint)
            throw new PromotionException("Plan identity, manifest, Local, or backup changed. No External writes performed.");
        return plan;
    }
    private Journal LoadJournal(PromotionPlan plan)
    {
        var journal = StateStore.Load<Journal>(JournalFile);
        if (journal.OperationId != plan.OperationId || journal.PlanHash != StateStore.Hash(plan)) throw new PromotionException("Journal does not match plan.");
        return journal;
    }
    private void Save(Journal journal) { journal.UpdatedUtc = DateTime.UtcNow; StateStore.Save(JournalFile, journal); }
    private FileStream FileLock() => new(Path.Combine(stateDirectory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static void RequireCurrent(Snapshot target, string slug, PageBaseline baseline, long? allowedRevision = null)
    {
        var page = target.Page(slug) ?? throw new PromotionException($"External page disappeared: {slug}.");
        if (page["page_id"] != baseline.Page["page_id"] || page["page_key"] != baseline.Page["page_key"] || page["page_slug"] != baseline.Page["page_slug"]
            || page.Number("page_current_revision_id") != (allowedRevision ?? baseline.Current.Number("revision_id")))
            throw new PromotionException($"External page changed since planning: {slug}. No newer revision will be overwritten.");
        PreserveHistory(target, slug, baseline);
    }
    private static void PreserveHistory(Snapshot target, string slug, PageBaseline baseline)
    {
        foreach (var (table, rows) in baseline.History)
        {
            var hashes = target.Rows(table).Select(StateStore.Hash).ToHashSet();
            if (rows.Any(row => !hashes.Contains(StateStore.Hash(row)))) throw new PromotionException($"Historical data changed: {slug}.");
        }
        var page = target.Page(slug)!;
        if (StateStore.Hash(target.Related("content_redirect", "redirect_page_id", page["page_id"])) != StateStore.Hash(baseline.Redirects)) throw new PromotionException($"Redirects changed: {slug}.");
        foreach (var (table, rows) in new[] { ("content_page_asset", baseline.Attachments), ("content_page_asset_dependency", baseline.GlobalDependencies) })
            if (rows.Any(row => !target.Rows(table).Any(r => StateStore.Hash(r) == StateStore.Hash(row)))) throw new PromotionException($"Preexisting dependencies changed: {slug}.");
    }
    public async Task Stage()
    {
        using var fileLock = FileLock(); await using var databaseLock = await external.WriterLock();
        var plan = await LoadPlan(); var target = await external.Read();
        var journal = File.Exists(JournalFile) ? LoadJournal(plan) : new Journal { OperationId = plan.OperationId, PlanHash = StateStore.Hash(plan) };
        if (journal.Status is "rolled-back" or "rollback-pending") throw new PromotionException("This operation is rolled back; create a fresh plan.");
        if (journal.Revisions.Count != 0) throw new PromotionException("Revision phase has started. Resume apply or rollback, not staging.");
        foreach (var (slug, baseline) in plan.Pages) RequireCurrent(target, slug, baseline);
        Save(journal); // Durable operation journal exists before ANY External service write.
        foreach (var item in plan.Entries.Where(e => e.Applicable))
        {
            target = await external.Read(); RequireCurrent(target, item.Manifest.Slug, plan.Pages[item.Manifest.Slug]);
            var assetState = journal.Assets.GetValueOrDefault(item.Manifest.LocalAssetKey);
            if (assetState != null && target.Asset(assetState.ExternalKey) == null)
            { journal.Assets.Remove(item.Manifest.LocalAssetKey); Save(journal); assetState = null; } // Journal flushed, transaction rolled back.
            if (assetState == null)
            {
                var interceptor = new MediaJournalInterceptor(async context =>
                {
                    var entry = context.ChangeTracker.Entries().Single(e => e.Metadata.GetTableName() == "content_asset");
                    var key = (string)entry.Property("AssetKey").CurrentValue!;
                    journal.Assets[item.Manifest.LocalAssetKey] = new StagedAsset { LocalKey = item.Manifest.LocalAssetKey, ExternalKey = key,
                        Url = $"/content/media/{key}/{entry.Property("FileName").CurrentValue}", Sha256 = item.Sha256, Created = true };
                    Save(journal); Fault?.Invoke("media-journal-before-commit"); await Task.CompletedTask;
                });
                var registry = new ServiceRegistry(external, [interceptor]);
                var assets = AssetService(registry);
                using var data = new MemoryStream(await local.Bytes(item.Manifest.LocalAssetKey));
                var actual = item.ExistingExternalKey is { } existingKey
                    ? await assets.GetInfoFromSourceAsync("External", existingKey) ?? throw new PromotionException("Planned reused media disappeared.")
                    : await assets.UploadAsync("External", item.FileName, item.MediaType, data, data.Length);
                Fault?.Invoke("after-upload-commit");
                if (!journal.Assets.TryGetValue(item.Manifest.LocalAssetKey, out assetState))
                    journal.Assets[item.Manifest.LocalAssetKey] = assetState = new StagedAsset { LocalKey = item.Manifest.LocalAssetKey, ExternalKey = actual.AssetKey, Url = actual.Url, Sha256 = actual.Sha256, Created = false };
                if (assetState.ExternalKey != actual.AssetKey || actual.Sha256 != item.Sha256) throw new PromotionException("Unexpected media service result.");
                Save(journal);
            }
            target = await external.Read();
            if (!target.Attached(target.Page(item.Manifest.Slug)!, assetState.ExternalKey))
            {
                var interceptor = new MediaJournalInterceptor(async context =>
                {
                    var tx = context.Database.CurrentTransaction!.GetDbTransaction();
                    var suffix = external.IsSqlite ? "" : " FOR UPDATE";
                    var current = await Database.Scalar(tx.Connection!, tx, "SELECT page_current_revision_id FROM content_page WHERE page_slug=@slug" + suffix, ("slug", item.Manifest.Slug));
                    if (Convert.ToInt64(current) != plan.Pages[item.Manifest.Slug].Current.Number("revision_id")) throw new PromotionException($"Page changed during staging: {item.Manifest.Slug}.");
                    var version = external.IsSqlite ? null : Convert.ToString(await Database.Scalar(tx.Connection!, tx,
                        "SELECT p.xmin::text FROM content_page_asset p JOIN content_asset a ON a.asset_id=p.asset_id WHERE p.page_id=@page AND a.asset_key=@key",
                        ("page", plan.Pages[item.Manifest.Slug].Page.Number("page_id")), ("key", assetState.ExternalKey)));
                    var link = new AddedDependency(item.Manifest.Slug, assetState.ExternalKey, version);
                    journal.AddedDependencies.RemoveAll(d => d.Slug == link.Slug && d.AssetKey == link.AssetKey);
                    journal.AddedDependencies.Add(link);
                    Save(journal); Fault?.Invoke("dependency-journal-before-commit");
                });
                await AssetService(new ServiceRegistry(external, [interceptor])).AttachAsync("External", item.Manifest.Slug, "External", assetState.ExternalKey);
                Fault?.Invoke("after-attachment-commit");
            }
        }
        target = await external.Read();
        foreach (var (slug, baseline) in plan.Pages) RequireCurrent(target, slug, baseline);
        await ValidateStaging(plan, journal, target);
        journal.StagingValidated = true; journal.Status = "staged"; Save(journal);
    }
    private static ContentAssetService AssetService(ServiceRegistry registry)
    {
        var accessor = new HttpContextAccessor();
        return new(registry, accessor, new ContentCatalogService(new CompositeContentRepository(accessor, registry)));
    }
    private async Task ValidateStaging(PromotionPlan plan, Journal journal, Snapshot target)
    {
        foreach (var item in plan.Entries.Where(e => e.Applicable))
        {
            if (!journal.Assets.TryGetValue(item.Manifest.LocalAssetKey, out var staged)) throw new PromotionException("Staging is incomplete.");
            var asset = target.Asset(staged.ExternalKey) ?? throw new PromotionException("Staged asset is missing.");
            if (StateStore.Url(asset) != staged.Url || asset["asset_sha256"] != item.Sha256 || StateStore.Sha(await external.Bytes(staged.ExternalKey)) != item.Sha256
                || !target.Attached(target.Page(item.Manifest.Slug)!, staged.ExternalKey)) throw new PromotionException($"Staging validation failed: {item.Manifest.Slug}.");
        }
    }
    private static string RevisionChecksum(Snapshot target, long id) => StateStore.Hash(new[] { "content_revision", "content_revision_tag", "content_revision_mode", "content_revision_asset" }
        .ToDictionary(table => table, table => target.Related(table, "revision_id", id.ToString())));
    private static void CheckNewRevision(Snapshot target, string slug, PageBaseline baseline, RevisionChange change)
    {
        var current = target.Current(target.Page(slug)!);
        if (current.Text("revision_body") != change.ExpectedBody || !References.SameMetadata(current.Text("revision_metadata_json"), change.ExpectedMetadata)
            || current["revision_body_format"] != baseline.Current["revision_body_format"] || current["revision_parent_id"] != baseline.Current["revision_id"])
            throw new PromotionException($"Unexpected body, metadata or history change: {slug}.");
        foreach (var (table, value) in new[] { ("content_revision_tag", "tag"), ("content_revision_mode", "site_mode") })
        {
            var previous = baseline.History[table].Where(r => r["revision_id"] == baseline.Current["revision_id"]).Select(r => r[value]).ToHashSet();
            if (!previous.SetEquals(target.Related(table, "revision_id", current["revision_id"]).Select(r => r[value]))) throw new PromotionException($"Tags, listing state or modes changed: {slug}.");
        }
        PreserveHistory(target, slug, baseline);
    }
    public async Task Apply()
    {
        using var fileLock = FileLock(); await using var databaseLock = await external.WriterLock();
        var plan = await LoadPlan(); var journal = LoadJournal(plan);
        if (!journal.StagingValidated || journal.Status is "rollback-pending" or "rolled-back") throw new PromotionException("Validated staging is required before apply.");
        var target = await external.Read(); await ValidateStaging(plan, journal, target);
        journal.Status = "applying"; Save(journal);
        foreach (var (slug, baseline) in plan.Pages)
        {
            var change = journal.Revisions.GetValueOrDefault(slug);
            if (change?.CreatedId is { } id && target.Rows("content_revision").Any(r => r.Number("revision_id") == id)) RequireCurrent(target, slug, baseline, id);
            else RequireCurrent(target, slug, baseline);
        }
        foreach (var (slug, baseline) in plan.Pages)
        {
            target = await external.Read();
            if (journal.Revisions.TryGetValue(slug, out var existing) && existing.CreatedId is { } committedId && target.Rows("content_revision").Any(r => r.Number("revision_id") == committedId))
            {
                RequireCurrent(target, slug, baseline, committedId); CheckNewRevision(target, slug, baseline, existing);
                if (RevisionChecksum(target, committedId) != existing.CreatedChecksum) throw new PromotionException("Migration revision changed after journaling.");
                existing.Status = "complete"; Save(journal); continue;
            }
            RequireCurrent(target, slug, baseline);
            // Only the current EXTERNAL editor document is passed to the authoring service.
            var edit = await new ContentAuthoringService(new ServiceRegistry(external)).GetEditAsync("External", slug) ?? throw new PromotionException("External page is missing.");
            var document = edit.Document;
            if (document.ExpectedRevisionId != baseline.Current.Number("revision_id") || document.Body != baseline.Current.Text("revision_body")
                || !References.SameMetadata(document.MetadataJson, baseline.Current.Text("revision_metadata_json")))
                throw new PromotionException($"External changed or authoring would normalize unknown metadata: {slug}.");
            foreach (var item in plan.Entries.Where(e => e.Applicable && e.Manifest.Slug == slug))
            {
                var url = journal.Assets[item.Manifest.LocalAssetKey].Url;
                document.Body = References.Replace(document.Body, item.Manifest.OldUrl, url);
                document.MetadataJson = References.Metadata(document.MetadataJson, s => References.Replace(s, item.Manifest.OldUrl, url));
            }
            var change = new RevisionChange { PreviousId = document.ExpectedRevisionId, ExpectedBody = document.Body, ExpectedMetadata = document.MetadataJson };
            journal.Revisions[slug] = change; Save(journal);
            if (document.Body == baseline.Current.Text("revision_body") && References.SameMetadata(document.MetadataJson, baseline.Current.Text("revision_metadata_json")))
            { change.Status = "unchanged"; Save(journal); continue; }
            var interceptor = new RevisionJournalInterceptor(external, slug, document.ExpectedRevisionId, async (c, tx) =>
            {
                var after = await Database.Read(c, tx);
                CheckNewRevision(after, slug, baseline, change);
                change.CreatedId = after.Current(after.Page(slug)!).Number("revision_id"); change.CreatedChecksum = RevisionChecksum(after, change.CreatedId.Value);
                change.Status = "commit-pending"; Save(journal); Fault?.Invoke("revision-journal-before-commit");
            });
            await new ContentAuthoringService(new ServiceRegistry(external, [interceptor, new PreserveRevisionLabels(baseline)])).SaveRevisionAsync(document);
            Fault?.Invoke("after-revision-commit");
            change.Status = "complete"; Save(journal);
        }
        journal.Status = "applied"; Save(journal);
    }
    public async Task<(PromotionPlan Plan, Journal Journal, Snapshot Target)> VerifyDatabase()
    {
        var plan = await LoadPlan(); var journal = LoadJournal(plan); var target = await external.Read();
        if (journal.Status != "applied") throw new PromotionException("Apply has not completed; no global verification success is possible.");
        await ValidateStaging(plan, journal, target);
        foreach (var (slug, baseline) in plan.Pages)
        {
            var change = journal.Revisions[slug]; RequireCurrent(target, slug, baseline, change.CreatedId);
            if (change.CreatedId is { } id) { CheckNewRevision(target, slug, baseline, change); if (RevisionChecksum(target, id) != change.CreatedChecksum) throw new PromotionException("Migration revision checksum changed."); }
        }
        foreach (var item in plan.Entries.Where(e => e.Applicable))
        {
            var page = target.Page(item.Manifest.Slug)!; var revision = target.Current(page); var url = journal.Assets[item.Manifest.LocalAssetKey].Url;
            if (References.Contains(revision.Text("revision_body"), item.Manifest.OldUrl) || References.InMetadata(revision.Text("revision_metadata_json"), item.Manifest.OldUrl)
                || (!References.Contains(revision.Text("revision_body"), url) && !References.InMetadata(revision.Text("revision_metadata_json"), url))
                || !target.Rows("content_revision_asset").Any(r => r["revision_id"] == revision["revision_id"] && r["asset_key"] == journal.Assets[item.Manifest.LocalAssetKey].ExternalKey))
                throw new PromotionException($"Current reference verification failed: {item.Manifest.Slug}.");
        }
        return (plan, journal, target);
    }
    public async Task Rollback()
    {
        using var fileLock = FileLock(); await using var databaseLock = await external.WriterLock();
        var plan = await LoadPlan(); var journal = LoadJournal(plan);
        if (journal.Status == "rolled-back") return;
        await using var c = external.Connection(false); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        foreach (var slug in plan.Pages.Keys.Order(StringComparer.Ordinal))
            await Database.Scalar(c, tx, "SELECT page_current_revision_id FROM content_page WHERE page_slug=@slug" + (external.IsSqlite ? "" : " FOR UPDATE"), ("slug", slug));
        var target = await Database.Read(c, tx);
        foreach (var (slug, baseline) in plan.Pages)
        {
            var change = journal.Revisions.GetValueOrDefault(slug);
            bool exists = change?.CreatedId is { } id && target.Rows("content_revision").Any(r => r.Number("revision_id") == id);
            RequireCurrent(target, slug, baseline, exists ? change!.CreatedId : null);
            if (exists)
            {
                if (target.Rows("content_revision").Any(r => r["revision_parent_id"] == change!.CreatedId!.Value.ToString())) throw new PromotionException($"Subsequent revision depends on migration: {slug}.");
                if (RevisionChecksum(target, change!.CreatedId!.Value) != change.CreatedChecksum) throw new PromotionException("Rollback revision checksum mismatch.");
            }
        }
        // A detached/re-attached PostgreSQL link belongs to a different transaction.
        // Refuse to remove it even when its page/key happen to match our journal.
        foreach (var dependency in journal.AddedDependencies.Where(d => d.Version != null))
        {
            var version = await Database.Scalar(c, tx,
                "SELECT p.xmin::text FROM content_page_asset p JOIN content_asset a ON a.asset_id=p.asset_id WHERE p.page_id=@page AND a.asset_key=@key FOR UPDATE OF p",
                ("page", plan.Pages[dependency.Slug].Page.Number("page_id")), ("key", dependency.AssetKey));
            if (version != null && Convert.ToString(version) != dependency.Version)
                throw new PromotionException($"Migration dependency changed ownership: {dependency.Slug}. Rollback refused.");
        }
        journal.Status = "rollback-pending"; Save(journal);
        foreach (var (slug, change) in journal.Revisions.Where(p => p.Value.CreatedId != null))
        {
            if (!target.Rows("content_revision").Any(r => r.Number("revision_id") == change.CreatedId)) continue;
            await Database.Execute(c, tx, "UPDATE content_page SET page_current_revision_id=@previous WHERE page_slug=@slug AND page_current_revision_id=@created", ("previous", change.PreviousId), ("slug", slug), ("created", change.CreatedId));
            await Database.Execute(c, tx, "DELETE FROM content_revision WHERE revision_id=@created AND revision_page_id=@page", ("created", change.CreatedId), ("page", plan.Pages[slug].Page.Number("page_id")));
        }
        foreach (var dependency in journal.AddedDependencies)
            await Database.Execute(c, tx, "DELETE FROM content_page_asset WHERE page_id=@page AND asset_id=(SELECT asset_id FROM content_asset WHERE asset_key=@key) AND relationship='attached'",
                ("page", plan.Pages[dependency.Slug].Page.Number("page_id")), ("key", dependency.AssetKey));
        var localSnapshot = await local.Read();
        foreach (var asset in journal.Assets.Values.Where(a => a.Created))
        {
            if (target.Asset(asset.ExternalKey) is { } row && row["asset_sha256"] != asset.Sha256) throw new PromotionException("Refusing to delete modified media.");
            if (localSnapshot.Rows("content_revision_asset").Any(r => r["asset_key"] == asset.ExternalKey)
                || localSnapshot.Rows("content_page_asset_dependency").Any(r => r["asset_key"] == asset.ExternalKey)) continue;
            await Database.Execute(c, tx, "DELETE FROM content_asset WHERE asset_key=@key AND NOT EXISTS (SELECT 1 FROM content_page_asset p WHERE p.asset_id=content_asset.asset_id) AND NOT EXISTS (SELECT 1 FROM content_revision_asset r WHERE r.asset_key=content_asset.asset_key) AND NOT EXISTS (SELECT 1 FROM content_page_asset_dependency d WHERE d.asset_key=content_asset.asset_key)", ("key", asset.ExternalKey));
        }
        Fault?.Invoke("rollback-before-commit"); await tx.CommitAsync(); Fault?.Invoke("after-rollback-commit");
        journal.Status = "rolled-back"; Save(journal);
    }
}
