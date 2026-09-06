using System.Diagnostics;
using System.Text.Json;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ContentMediaExternalPromotion;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? state = null;
        try
        {
            if (args.Contains("--help"))
            {
                Console.WriteLine("ContentMediaExternalPromotion [--plan|--stage-assets|--apply-revisions|--verify|--rollback] [--root PATH] [--local PATH] [--backup PATH] [--state IGNORED_DIRECTORY]");
                Console.WriteLine("Defaults to --plan. Plan and verify open content databases read-only. Stage/apply/rollback are explicit writes to configured External only.");
                return 0;
            }
            var modes = args.Where(a => new[] { "--plan", "--stage-assets", "--apply-revisions", "--verify", "--rollback" }.Contains(a)).ToArray();
            if (modes.Length > 1) throw new PromotionException("Select exactly one operation.");
            var mode = modes.SingleOrDefault() ?? "--plan";
            var values = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (modes.Contains(args[i])) continue;
                if (!new[] { "--root", "--local", "--backup", "--state" }.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new PromotionException("Unknown or incomplete argument. Use --help.");
                if (!values.TryAdd(args[i], args[++i])) throw new PromotionException("Duplicate argument.");
            }
            var root = Path.GetFullPath(values.GetValueOrDefault("--root") ?? FindRoot());
            var app = Path.Combine(root, "dorks-and-dice-site");
            var expectedLocal = Path.GetFullPath(Path.Combine(app, "Content", "content.db"));
            var localPath = Path.GetFullPath(values.GetValueOrDefault("--local") ?? expectedLocal);
            if (!string.Equals(localPath, expectedLocal, StringComparison.OrdinalIgnoreCase)) throw new PromotionException("Local must be the exact checkout Content/content.db file.");
            var originalBackup = Path.Combine(root, "work", "content-media-migration", "local-before-20260906T055928Z.db");
            var preservedBackup = Path.Combine(root, ".tmp", "content-media-backups", "local-before-20260906T055928Z.db");
            var backupPath = Path.GetFullPath(values.GetValueOrDefault("--backup") ?? (File.Exists(originalBackup) ? originalBackup : preservedBackup));
            if (!File.Exists(localPath) || !File.Exists(backupPath)) throw new PromotionException("Local or pre-migration backup is missing. Supply the preserved backup with --backup.");
            state = Path.GetFullPath(values.GetValueOrDefault("--state") ?? Path.Combine(root, ".tmp", "content-media-external-promotion"));
            if (state.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && await Git(root, "check-ignore", "-q", "--", Path.Combine(state, "probe.json")) != 0)
                throw new PromotionException("State must be outside the repository or inside a Git-ignored directory.");
            var configuration = new ConfigurationBuilder().SetBasePath(app).AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json").AddUserSecrets(typeof(ContentAssetService).Assembly).AddEnvironmentVariables().Build();
            var registry = new ContentSourceRegistry(configuration, app);
            var source = registry.GetSource("Local"); var destination = registry.GetSource("External");
            if (!source.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFullPath(new SqliteConnectionStringBuilder(source.ConnectionString).DataSource), expectedLocal, StringComparison.OrdinalIgnoreCase)) throw new PromotionException("Configured Local identity is not the expected SQLite file.");
            if (!new[] { "postgres", "postgresql" }.Contains(destination.Provider.ToLowerInvariant())) throw new PromotionException("This one-time production tool expects External to be PostgreSQL.");
            if (registry.GetKnownSourceKeys().Any(k => k != "Local" && k != "External")) throw new PromotionException("Additional content sources require separate dependency review; this tool is scoped to Local and External only.");
            if (mode is "--stage-assets" or "--apply-revisions" or "--rollback")
            {
                var branch = await GitText(FindToolRoot(), "branch", "--show-current");
                if (branch.Trim() != "migration/promote-content-media-to-external") throw new PromotionException("Writes require the temporary promotion branch.");
            }
            var manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "manifest.json")), StateStore.Json) ?? [];
            if (manifest.Count != 22) throw new PromotionException("Expected the explicit 22-entry migration manifest.");
            var local = new Database("Sqlite", new SqliteConnectionStringBuilder { DataSource = localPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
            var backup = new Database("Sqlite", new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
            var external = new Database(destination.Provider, destination.ConnectionString);
            Console.WriteLine($"External provider: {external.Provider}; database identity fingerprint: {external.Identity}. Credentials are never printed.");
            var engine = new PromotionEngine(local, backup, external, Path.Combine(app, "wwwroot"), state, manifest);
            switch (mode)
            {
                case "--plan":
                    var plan = await engine.Plan();
                    foreach (var group in plan.Entries.GroupBy(e => e.Manifest.Slug))
                    {
                        Console.WriteLine($"{group.Key}: {(group.All(e => e.MissingPage) ? "MISSING / skipped" : "inspected")}");
                        foreach (var entry in group) Console.WriteLine($"  {entry.Manifest.OldUrl} | {entry.Manifest.LocalAssetKey} | SHA-256 {entry.Sha256} | body={entry.BodyReference}, metadata={entry.MetadataReference}, dependency={entry.ExistingDependency}, identical-media={entry.ExistingExternalKey ?? "none"}, proposed={entry.ProposedExternalUrl ?? "assigned during staging"}, safe={entry.Safe}");
                    }
                    Console.WriteLine($"Plan saved. {plan.Entries.Count} entries; {plan.Pages.Count} applicable existing External pages; no database writes.");
                    break;
                case "--stage-assets": await engine.Stage(); Report(state); break;
                case "--apply-revisions": await engine.Apply(); Report(state); break;
                case "--rollback": await engine.Rollback(); Report(state); break;
                case "--verify":
                    var beforeLocal = (await local.Read()).Fingerprint; var beforeExternal = (await external.Read()).Fingerprint;
                    var verified = await engine.VerifyDatabase();
                    var count = await HttpVerification.Run(root, state, registry, configuration, verified.Plan, verified.Journal);
                    await HttpVerification.RunApplicationTests(root, state);
                    if ((await local.Read()).Fingerprint != beforeLocal || (await external.Read()).Fingerprint != beforeExternal) throw new PromotionException("A database changed during verification; repeat verification against stable state.");
                    Report(state);
                    Console.WriteLine($"SHA-256 and database verification passed; {count} HTTP checks passed; normal application test suite passed.");
                    var remaining = verified.Target.Rows("content_page").Where(p => p["page_current_revision_id"] != null).Select(p => (Page: p, Revision: verified.Target.Current(p)))
                        .Where(x => Uri.UnescapeDataString(x.Revision.Text("revision_body")).Contains("/site-modes/", StringComparison.OrdinalIgnoreCase) || References.Metadata(x.Revision.Text("revision_metadata_json"), s => Uri.UnescapeDataString(s)).Contains("/site-modes/", StringComparison.OrdinalIgnoreCase)).Select(x => x.Page.Text("page_slug")).ToArray();
                    Console.WriteLine($"Remaining current authored static-reference pages: {(remaining.Length == 0 ? "none" : string.Join(", ", remaining))}");
                    break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is PromotionException ? ex.Message : $"Operation failed ({ex.GetType().Name}). No global success is reported. Check configuration/state and retry the same operation; credentials and provider exception text are suppressed.");
            if (state != null && File.Exists(Path.Combine(state, "journal.json")))
                try { Report(state); } catch { Console.Error.WriteLine("Journal could not be read; do not discard it."); }
            return 1;
        }
    }
    private static void Report(string state)
    {
        var plan = StateStore.Load<PromotionPlan>(Path.Combine(state, "plan.json")); var journal = StateStore.Load<Journal>(Path.Combine(state, "journal.json"));
        var distinctAssets = journal.Assets.Values.GroupBy(a => a.ExternalKey).ToArray();
        Console.WriteLine($"Operation {plan.OperationId}: {journal.Status}; manifest entries={plan.Entries.Count}; pages revised={journal.Revisions.Values.Count(r => r.Status == "complete")}; missing pages={plan.Entries.Where(e => e.MissingPage).Select(e => e.Manifest.Slug).Distinct().Count()}.");
        Console.WriteLine($"Journal records: assets created={distinctAssets.Count(g => g.Any(a => a.Created))}, reused={distinctAssets.Count(g => g.All(a => !a.Created))}; dependencies added={journal.AddedDependencies.Count}.");
        if (journal.Status is not ("staged" or "applied" or "rolled-back")) Console.WriteLine("Operation incomplete. Journaled writes may still be pending or rolled back; rerun the same phase to reconcile exact database outcomes.");
        if (journal.Status == "rolled-back") Console.WriteLine("Rollback completed. Counts and revision IDs below describe the original operation, not active changes; newly created assets still used elsewhere were retained.");
        foreach (var (slug, change) in journal.Revisions) Console.WriteLine($"  {slug}: {change.Status}, previous={change.PreviousId}, migration={change.CreatedId?.ToString() ?? "none"}");
        foreach (var slug in plan.Pages.Keys.Except(journal.Revisions.Keys)) Console.WriteLine($"  {slug}: revision phase not completed");
        foreach (var group in plan.Entries.Where(e => !e.Applicable).GroupBy(e => e.Manifest.Slug)) Console.WriteLine($"  {group.Key}: skipped ({group.First().Reason})");
        var removed = plan.Entries.Where(e => e.Applicable && journal.Revisions.GetValueOrDefault(e.Manifest.Slug)?.Status == "complete").Sum(e => References.Count(plan.Pages[e.Manifest.Slug].Current.Text("revision_body"), e.Manifest.OldUrl) + MetadataCount(plan.Pages[e.Manifest.Slug].Current.Text("revision_metadata_json"), e.Manifest.OldUrl));
        Console.WriteLine($"Manifest-approved old static references removed by completed revisions: {(journal.Status == "rolled-back" ? 0 : removed)}.");
    }
    private static int MetadataCount(string json, string old) { int count = 0; References.Metadata(json, s => { count += References.Count(s, old); return s; }); return count; }
    private static string FindToolRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "tools", "ContentMediaExternalPromotion", "ContentMediaExternalPromotion.csproj"))) return directory.FullName;
        throw new PromotionException("Run the tool from its temporary branch checkout.");
    }
    private static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "dorks-and-dice-site.slnx"))) return directory.FullName;
        throw new PromotionException("Cannot find checkout. Supply --root.");
    }
    private static async Task<int> Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!; await process.WaitForExitAsync(); return process.ExitCode;
    }
    private static async Task<string> GitText(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!; var result = await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new PromotionException("Could not inspect Git branch."); return result;
    }
}
