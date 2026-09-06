using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json.Nodes;
using ContentMediaExternalPromotion;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ContentMediaExternalPromotion.Tests;

public sealed class PromotionTests
{
    [Fact]
    public async Task PlanIsReadOnlyAndReportsMissingTargets()
    {
        await using var f = await Fixture.Create(missingExternal: true);
        var before = (await f.External.Read()).Fingerprint;
        var sourceBefore = (await f.Local.Read()).Fingerprint;
        var plan = await f.Engine.Plan();
        Assert.True(Assert.Single(plan.Entries).MissingPage);
        Assert.Empty(plan.Pages);
        await f.Engine.Stage(); await f.Engine.Apply(); await f.Engine.VerifyDatabase();
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
        Assert.Equal(sourceBefore, (await f.Local.Read()).Fingerprint);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StagingAndApplyAreIdempotentAndPreserveExternalAuthority(bool reuse)
    {
        await using var f = await Fixture.Create(reuse: reuse);
        var before = await f.External.Read(); var pageBefore = before.Current(before.Page("target")!);
        var localBefore = (await f.Local.Read()).Fingerprint;
        var plan = await f.Engine.Plan(); Assert.True(plan.Entries[0].BodyReference);
        await f.Engine.Stage(); var staged = await f.External.Read();
        Assert.Equal(pageBefore.Text("revision_body"), staged.Current(staged.Page("target")!).Text("revision_body"));
        Assert.Equal(before.Rows("content_revision").Count, staged.Rows("content_revision").Count);
        await f.Engine.Stage(); Assert.Equal(staged.Fingerprint, (await f.External.Read()).Fingerprint);
        await f.Engine.Apply(); var applied = await f.Engine.VerifyDatabase();
        Assert.Equal(before.Rows("content_revision").Count + 1, applied.Target.Rows("content_revision").Count);
        var current = applied.Target.Current(applied.Target.Page("target")!);
        Assert.Contains("EXTERNAL authoritative text", current.Text("revision_body"));
        Assert.DoesNotContain("LOCAL unrelated text", current.Text("revision_body"));
        Assert.Contains("External title", current.Text("revision_metadata_json"));
        Assert.DoesNotContain("Local title", current.Text("revision_metadata_json"));
        Assert.DoesNotContain("/site-modes/", current.Text("revision_body"));
        Assert.Equal(!reuse, Assert.Single(applied.Journal.Assets.Values).Created);
        if (reuse) Assert.NotEqual(f.Manifest[0].LocalAssetKey, Assert.Single(applied.Journal.Assets.Values).ExternalKey);
        await f.Engine.Apply(); Assert.Equal(applied.Target.Fingerprint, (await f.External.Read()).Fingerprint);
        await f.Engine.Rollback(); Assert.Equal(before.Fingerprint, (await f.External.Read()).Fingerprint);
        await f.Engine.Rollback(); Assert.Equal(before.Fingerprint, (await f.External.Read()).Fingerprint);
        Assert.Equal(localBefore, (await f.Local.Read()).Fingerprint);
    }
    [Fact]
    public async Task ExternalEditAfterPlanningBlocksStagingWithoutChanges()
    {
        await using var f = await Fixture.Create(); await f.Engine.Plan(); await f.EditExternal("Newer editor content");
        var before = (await f.External.Read()).Fingerprint;
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Stage());
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Fact]
    public async Task ExternalEditAfterStagingBlocksApplyAndRollback()
    {
        await using var f = await Fixture.Create(); await f.Engine.Plan(); await f.Engine.Stage(); await f.EditExternal("Subsequent editorial work");
        var before = (await f.External.Read()).Fingerprint;
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Apply());
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Rollback());
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Fact]
    public async Task RollbackRefusesLaterRevisionDependingOnMigration()
    {
        await using var f = await Fixture.Create(); await f.Engine.Plan(); await f.Engine.Stage(); await f.Engine.Apply();
        await f.EditExternal("After migration"); var before = (await f.External.Read()).Fingerprint;
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Rollback());
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Theory]
    [InlineData("media-journal-before-commit")]
    [InlineData("after-upload-commit")]
    [InlineData("dependency-journal-before-commit")]
    [InlineData("after-attachment-commit")]
    public async Task InterruptedStagingResumesWithoutDuplicateAssetsOrDependencies(string point)
    {
        await using var f = await Fixture.Create(); var before = (await f.External.Read()).Fingerprint; await f.Engine.Plan();
        f.Engine.Fault = step => { if (step == point) throw new IOException("Simulated crash"); };
        await Assert.ThrowsAsync<IOException>(() => f.Engine.Stage());
        f.Engine.Fault = null; await f.Engine.Stage(); await f.Engine.Stage();
        var staged = await f.External.Read(); Assert.Single(staged.Rows("content_asset")); Assert.Single(staged.Rows("content_page_asset"));
        await f.Engine.Apply(); await f.Engine.VerifyDatabase(); await f.Engine.Rollback();
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Theory]
    [InlineData("revision-journal-before-commit")]
    [InlineData("after-revision-commit")]
    public async Task InterruptedApplyResumesWithExactlyOneNewRevision(string point)
    {
        await using var f = await Fixture.Create(); var before = await f.External.Read(); await f.Engine.Plan(); await f.Engine.Stage();
        f.Engine.Fault = step => { if (step == point) throw new IOException("Simulated crash"); };
        await Assert.ThrowsAsync<IOException>(() => f.Engine.Apply());
        f.Engine.Fault = null; await f.Engine.Apply(); await f.Engine.VerifyDatabase();
        Assert.Equal(before.Rows("content_revision").Count + 1, (await f.External.Read()).Rows("content_revision").Count);
        await f.Engine.Rollback(); Assert.Equal(before.Fingerprint, (await f.External.Read()).Fingerprint);
    }
    [Theory]
    [InlineData("rollback-before-commit")]
    [InlineData("after-rollback-commit")]
    public async Task InterruptedRollbackResumesSafely(string point)
    {
        await using var f = await Fixture.Create(); var before = (await f.External.Read()).Fingerprint; await f.Engine.Plan(); await f.Engine.Stage(); await f.Engine.Apply();
        f.Engine.Fault = step => { if (step == point) throw new IOException("Simulated crash"); };
        await Assert.ThrowsAsync<IOException>(() => f.Engine.Rollback());
        f.Engine.Fault = null; await f.Engine.Rollback();
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Fact]
    public async Task UnknownExternalMetadataAbortsRatherThanDroppingIt()
    {
        await using var f = await Fixture.Create();
        await using (var c = f.External.Connection(false))
        {
            await c.OpenAsync(); var target = await f.External.Read(); var revision = target.Current(target.Page("target")!);
            var node = JsonNode.Parse(revision.Text("revision_metadata_json"))!; node["futureField"] = "Must survive";
            await Database.Execute(c, null, "UPDATE content_revision SET revision_metadata_json=@json WHERE revision_id=@id", ("json", node.ToJsonString()), ("id", revision.Number("revision_id")));
        }
        await f.Engine.Plan(); await f.Engine.Stage(); var before = (await f.External.Read()).Fingerprint;
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Apply());
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Fact]
    public async Task CorruptLocalBytesAbortWholePlanBeforeAnyExternalWrite()
    {
        await using var f = await Fixture.Create(); var before = (await f.External.Read()).Fingerprint;
        await using var c = f.Local.Connection(false); await c.OpenAsync();
        await Database.Execute(c, null, "UPDATE content_asset SET asset_data=@data", ("data", new byte[] { 0, 1 }));
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Plan());
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }
    [Fact]
    public async Task MissingLocalDependencyAbortsWholePlan()
    {
        await using var f = await Fixture.Create();
        await using var c = f.Local.Connection(false); await c.OpenAsync(); await Database.Execute(c, null, "DELETE FROM content_page_asset");
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Plan());
    }
    [Fact]
    public async Task TamperedJournalCannotAuthorizeWrites()
    {
        await using var f = await Fixture.Create(); await f.Engine.Plan(); await f.Engine.Stage();
        var path = Path.Combine(f.State, "journal.json"); var text = await File.ReadAllTextAsync(path); await File.WriteAllTextAsync(path, text.Replace("staged", "applied"));
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.Apply());
    }
    [Fact]
    public void ReplacementIsBoundedAndDoesNotImportUnrelatedText()
    {
        const string old = "/site-modes/professional/files/File name.pdf";
        var input = $"[raw](<{old}>) [encoded](~/site-modes/professional/files/File%20name.pdf) keep https://other.example{old} keep {old}.bak";
        var result = References.Replace(input, old, "/content/media/key/new.pdf");
        Assert.Contains("[raw](</content/media/key/new.pdf>)", result);
        Assert.Contains("[encoded](/content/media/key/new.pdf)", result);
        Assert.Contains($"https://other.example{old}", result); Assert.Contains($"{old}.bak", result);
        Assert.Equal(2, References.Count(input, old));
    }
    [Fact]
    public async Task SharedMediaIsUploadedOnceAndRollbackPreservesOtherUsers()
    {
        await using var f = await Fixture.Create();
        await f.Engine.Plan(); await f.Engine.Stage(); await f.Engine.Apply();
        var state = await f.Engine.VerifyDatabase(); var asset = Assert.Single(state.Journal.Assets.Values);
        var registry = new ServiceRegistry(f.External); var author = new ContentAuthoringService(registry);
        var doc = author.GetNew("External").Document; doc.Id = "other-user"; doc.Slug = "other-user";
        await author.CreateAsync(doc);
        var accessor = new HttpContextAccessor();
        var assets = new ContentAssetService(registry, accessor, new ContentCatalogService(new CompositeContentRepository(accessor, registry)));
        await assets.AttachAsync("External", doc.Slug, "External", asset.ExternalKey);
        doc = (await author.GetEditAsync("External", doc.Slug))!.Document; doc.Body = $"![Shared]({asset.Url})";
        await author.SaveRevisionAsync(doc);
        await f.Engine.Rollback(); var after = await f.External.Read();
        Assert.NotNull(after.Asset(asset.ExternalKey)); Assert.True(after.Attached(after.Page("other-user")!, asset.ExternalKey));
    }
    [Fact]
    public async Task ReadOnlyApplicationHostVerifiesMediaPagesAndIsolationWithoutWrites()
    {
        await using var f = await Fixture.Create(); await f.Engine.Plan(); await f.Engine.Stage(); await f.Engine.Apply();
        var state = await f.Engine.VerifyDatabase();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ContentStorage:AuthoringSource"] = "Local", ["ContentStorage:Sources:Local:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Local:ConnectionString"] = "LocalDb", ["ConnectionStrings:LocalDb"] = f.Local.ConnectionString,
            ["ContentStorage:Sources:External:Provider"] = f.External.Provider, ["ContentStorage:Sources:External:ConnectionString"] = "ExternalDb",
            ["ConnectionStrings:ExternalDb"] = f.External.ConnectionString, ["ContentStorage:GlobalSources:0"] = "External"
        }).Build();
        var root = new DirectoryInfo(f.DirectoryPath).Parent!.Parent!.Parent!.FullName;
        var registry = new ContentSourceRegistry(config, Path.Combine(root, "dorks-and-dice-site"));
        var localBefore = (await f.Local.Read()).Fingerprint; var externalBefore = (await f.External.Read()).Fingerprint;
        Assert.True(await HttpVerification.Run(root, f.State, registry, config, state.Plan, state.Journal) >= 10);
        Assert.Equal(localBefore, (await f.Local.Read()).Fingerprint); Assert.Equal(externalBefore, (await f.External.Read()).Fingerprint);
    }

    [Fact]
    public async Task NewRevisionPreservesLegacyModeSpellingExactly()
    {
        await using var f = await Fixture.Create();
        await using var c = f.External.Connection(false); await c.OpenAsync();
        await Database.Execute(c, null, "UPDATE content_revision_mode SET site_mode='Professional'");
        await f.Engine.Plan(); await f.Engine.Stage(); await f.Engine.Apply();
        var verified = await f.Engine.VerifyDatabase();
        Assert.All(verified.Target.Rows("content_revision_mode"), row => Assert.Equal("Professional", row.Text("site_mode")));
        await f.Engine.Rollback();
    }

    [Fact]
    public async Task SharedManifestAssetAcrossPagesResumesPartialApplyWithoutGlobalSuccess()
    {
        await using var f = await Fixture.Create();
        var item = f.Manifest[0];
        var localAsset = (await f.Local.Read()).Asset(item.LocalAssetKey)!;
        foreach (var database in new[] { f.Local, f.External })
        {
            var registry = new ServiceRegistry(database);
            var author = new ContentAuthoringService(registry);
            var doc = author.GetNew("External").Document;
            doc.Id = "stable-second"; doc.Slug = "second"; doc.Body = "Different authoritative content";
            await author.CreateAsync(doc);
            if (database == f.Local)
            {
                var accessor = new HttpContextAccessor();
                await new ContentAssetService(registry, accessor, new ContentCatalogService(new CompositeContentRepository(accessor, registry)))
                    .AttachAsync("External", "second", "External", item.LocalAssetKey);
            }
            doc = (await author.GetEditAsync("External", "second"))!.Document;
            doc.Body += "\n\n![Shared](" + (database == f.Local ? StateStore.Url(localAsset) : item.OldUrl) + ")";
            await author.SaveRevisionAsync(doc);
        }
        f.Manifest.Add(new ManifestEntry("second", item.OldUrl, item.LocalAssetKey));
        var before = (await f.External.Read()).Fingerprint;
        await f.Engine.Plan(); await f.Engine.Stage();
        var staged = await f.External.Read();
        Assert.Single(staged.Rows("content_asset")); Assert.Equal(2, staged.Rows("content_page_asset").Count);
        f.Engine.Fault = step => { if (step == "after-revision-commit") throw new IOException("Partial two-page operation"); };
        await Assert.ThrowsAsync<IOException>(() => f.Engine.Apply());
        await Assert.ThrowsAsync<PromotionException>(() => f.Engine.VerifyDatabase());
        f.Engine.Fault = null; await f.Engine.Apply();
        var verified = await f.Engine.VerifyDatabase();
        Assert.Equal(2, verified.Journal.Revisions.Values.Count(r => r.Status == "complete"));
        Assert.Single(verified.Journal.Assets);
        await f.Engine.Rollback();
        Assert.Equal(before, (await f.External.Read()).Fingerprint);
    }

    [Fact]
    public async Task ReadOnlyConnectionRejectsWrites()
    {
        await using var f = await Fixture.Create();
        await using var c = f.Local.Connection(true); await c.OpenAsync();
        await Assert.ThrowsAsync<SqliteException>(() => Database.Execute(c, null, "DELETE FROM content_page"));
    }
}

internal sealed class Fixture : IAsyncDisposable
{
    public required string DirectoryPath { get; init; }
    public required Database Local { get; init; }
    public required Database External { get; init; }
    public required PromotionEngine Engine { get; init; }
    public required List<ManifestEntry> Manifest { get; init; }
    public string State => Path.Combine(DirectoryPath, "state");
    public static async Task<Fixture> Create(bool missingExternal = false, bool reuse = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root != null && !File.Exists(Path.Combine(root.FullName, "dorks-and-dice-site.slnx"))) root = root.Parent;
        var folder = Path.Combine(root!.FullName, ".tmp", "promotion-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        Database Db(string name) => new("Sqlite", $"Data Source={Path.Combine(folder, name + ".db")};Pooling=False;Foreign Keys=True");
        var local = Db("local"); var external = Db("external"); var backup = Db("backup");
        if (Environment.GetEnvironmentVariable("PROMOTION_TEST_POSTGRES") is { Length: > 0 } testConnection)
        {
            var settings = new NpgsqlConnectionStringBuilder(testConnection);
            if (settings.Host != "127.0.0.1" || settings.Database != "postgres") throw new Exception("Tests require the disposable loopback PostgreSQL admin database.");
            var name = "promotion_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(testConnection); await admin.OpenAsync();
            await Database.Execute(admin, null, "CREATE DATABASE " + Database.Quote(name));
            settings.Database = name; settings.Pooling = false;
            external = new Database("PostgreSQL", settings.ConnectionString);
        }
        foreach (var database in new[] { local, external })
        { var options = new DbContextOptionsBuilder<ContentDbContext>();
          if (database.IsSqlite) options.UseSqlite(database.ConnectionString); else options.UseNpgsql(database.ConnectionString);
          await using var context = new ContentDbContext(options.Options); await context.Database.EnsureCreatedAsync(); }
        const string oldUrl = "/site-modes/professional/images/test.png";
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 10 };
        async Task MakePage(Database db, string title, string body)
        {
            var author = new ContentAuthoringService(new ServiceRegistry(db)); var doc = author.GetNew("External").Document;
            doc.Id = "stable-target"; doc.Slug = "target"; doc.Body = body;
            var meta = JsonNode.Parse(doc.MetadataJson)!; meta["title"] = title; doc.MetadataJson = meta.ToJsonString();
            await author.CreateAsync(doc);
        }
        await MakePage(local, "Local title", $"LOCAL unrelated text\n\n![Image]({oldUrl})");
        await using (var source = (SqliteConnection)local.Connection(false))
        await using (var destination = (SqliteConnection)backup.Connection(false))
        { await source.OpenAsync(); await destination.OpenAsync(); source.BackupDatabase(destination); }
        var localRegistry = new ServiceRegistry(local); var accessor = new HttpContextAccessor();
        var localAssets = new ContentAssetService(localRegistry, accessor, new ContentCatalogService(new CompositeContentRepository(accessor, localRegistry)));
        using var input = new MemoryStream(bytes); var asset = await localAssets.UploadAsync("External", "test.png", "image/png", input, bytes.Length);
        await localAssets.AttachAsync("External", "target", "External", asset.AssetKey);
        var authoring = new ContentAuthoringService(localRegistry); var edit = (await authoring.GetEditAsync("External", "target"))!.Document;
        edit.Body = edit.Body.Replace(oldUrl, asset.Url); await authoring.SaveRevisionAsync(edit);
        if (!missingExternal)
        {
            await MakePage(external, "External title", "Historical external body");
            var externalAuthor = new ContentAuthoringService(new ServiceRegistry(external)); var doc = (await externalAuthor.GetEditAsync("External", "target"))!.Document;
            doc.Body = $"EXTERNAL authoritative text\n\n![Image]({oldUrl})"; await externalAuthor.SaveRevisionAsync(doc);
        }
        if (reuse)
        {
            var registry = new ServiceRegistry(external); using var data = new MemoryStream(bytes);
            await new ContentAssetService(registry, accessor, new ContentCatalogService(new CompositeContentRepository(accessor, registry))).UploadAsync("External", "preexisting.png", "image/png", data, bytes.Length);
        }
        var web = Path.Combine(folder, "web"); var staticPath = Path.Combine(web, oldUrl.TrimStart('/')); System.IO.Directory.CreateDirectory(Path.GetDirectoryName(staticPath)!); await File.WriteAllBytesAsync(staticPath, bytes);
        var manifest = new List<ManifestEntry> { new("target", oldUrl, asset.AssetKey) };
        return new Fixture { DirectoryPath = folder, Local = local, External = external, Manifest = manifest,
            Engine = new PromotionEngine(local, backup, external, web, Path.Combine(folder, "state"), manifest) };
    }
    public async Task EditExternal(string text)
    {
        var author = new ContentAuthoringService(new ServiceRegistry(External)); var doc = (await author.GetEditAsync("External", "target"))!.Document;
        doc.Body += "\n\n" + text; await author.SaveRevisionAsync(doc);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // Ignored evidence remains inspectable; never touch a configured database.
}
