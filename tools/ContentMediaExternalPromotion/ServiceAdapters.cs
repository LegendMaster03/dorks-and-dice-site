using System.Data.Common;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Models.Site;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContentMediaExternalPromotion;

// Disposable console-only adapter. Website registration and source-selection policy are untouched.
internal sealed class ServiceRegistry(Database external, IEnumerable<IInterceptor>? interceptors = null) : IContentSourceRegistry
{
    public string AuthoringSourceKey => "External";
    public ContentSourceDefinition GetSource(string key) => key == "External"
        ? new("External", "External", external.Provider, external.ConnectionString)
        : throw new PromotionException("The write service cannot access Local or other sources.");
    public IReadOnlyList<ContentSourceDefinition> GetAllSources() => [GetSource("External")];
    public IReadOnlyList<ContentSourceDefinition> GetGlobalSources() => GetAllSources();
    public bool IsGlobalSource(string key) => key == "External";
    public IReadOnlySet<string> GetKnownSourceKeys() => new HashSet<string> { "External" };
    public IReadOnlyList<ContentSourceDefinition> GetSourcesByKeys(IEnumerable<string> keys) => keys.Select(GetSource).ToList();
    public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(string modeId) => GetAllSources();
    public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(SiteMode mode) => GetAllSources();
    public void ConfigureDbContext(DbContextOptionsBuilder options, string sourceKey)
    {
        _ = GetSource(sourceKey);
        if (external.IsSqlite) options.UseSqlite(external.ConnectionString); else options.UseNpgsql(external.ConnectionString);
        if (interceptors != null) options.AddInterceptors(interceptors);
    }
}

// Media service writes normally consist of a single implicit SaveChanges transaction.
// Make that transaction explicit so the journal is flushed BEFORE committing newly created keys/links.
internal sealed class MediaJournalInterceptor(Func<DbContext, Task> beforeCommit) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        await e.Context!.Database.BeginTransactionAsync(ct);
        return result;
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        await beforeCommit(e.Context!);
        var tx = e.Context!.Database.CurrentTransaction!;
        await tx.CommitAsync(ct); await tx.DisposeAsync();
        return result;
    }
}

// Lock/check the page inside the AUTHORING service's own transaction, closing the check/save race.
// Journal the precise new revision ID before that same transaction commits.
internal sealed class RevisionJournalInterceptor(Database database, string slug, long expectedId,
    Func<DbConnection, DbTransaction, Task> beforeCommit) : DbTransactionInterceptor
{
    public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData e, DbTransaction result, CancellationToken ct = default)
    {
        var suffix = database.IsSqlite ? "" : " FOR UPDATE";
        var id = await Database.Scalar(connection, result, "SELECT page_current_revision_id FROM content_page WHERE page_slug=@slug" + suffix, ("slug", slug));
        if (id == null || Convert.ToInt64(id) != expectedId) throw new PromotionException($"External page changed: {slug}. Re-plan before writing it.");
        return result;
    }
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData e, InterceptionResult result, CancellationToken ct = default)
    { await beforeCommit(transaction.Connection!, transaction); return result; }
}
// Preserve the exact legacy tag/mode spelling on newly added revision children.
// The authoring service still validates their meaning and owns revision creation.
internal sealed class PreserveRevisionLabels(PageBaseline baseline) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        foreach (var entry in e.Context!.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added))
        {
            var table = entry.Metadata.GetTableName();
            var property = table == "content_revision_mode" ? "SiteMode" : table == "content_revision_tag" ? "Tag" : null;
            if (property == null) continue;
            var column = property == "SiteMode" ? "site_mode" : "tag";
            var value = (string)entry.Property(property).CurrentValue!;
            string Normalize(string text) => (property == "SiteMode" ? text.Replace("-", "") : text).ToLowerInvariant();
            var original = baseline.History[table!].Where(r => r["revision_id"] == baseline.Current["revision_id"] && Normalize(r.Text(column)) == Normalize(value)).ToArray();
            if (original.Length != 1) throw new PromotionException("Cannot preserve ambiguous or changed revision labels.");
            entry.Property(property).CurrentValue = original[0].Text(column);
        }
        return ValueTask.FromResult(result);
    }
}
