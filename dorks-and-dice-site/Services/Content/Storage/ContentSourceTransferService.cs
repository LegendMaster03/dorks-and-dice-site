using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public interface IContentSourceTransferService
{
    Task CopyAsync(
        string sourceKey,
        string targetSourceKey,
        string slug,
        CancellationToken cancellationToken = default);

    Task<int> CopyAllAsync(
        string sourceKey,
        string targetSourceKey,
        CancellationToken cancellationToken = default);
}

public sealed class ContentSourceTransferService : IContentSourceTransferService
{
    private readonly IContentSourceRegistry _sourceRegistry;

    public ContentSourceTransferService(IContentSourceRegistry sourceRegistry)
    {
        _sourceRegistry = sourceRegistry;
    }

    public async Task CopyAsync(
        string sourceKey,
        string targetSourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        var (source, target) = ResolveDistinctSources(sourceKey, targetSourceKey);
        await using var sourceContext = CreateContext(source.Key);
        await using var targetContext = CreateContext(target.Key);
        await ContentStorageSchema.EnsureCurrentAsync(sourceContext, cancellationToken);
        await ContentStorageSchema.EnsureCurrentAsync(targetContext, cancellationToken);

        var sourcePage = await LoadPageAsync(sourceContext, slug, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Content page '{slug}' was not found in source '{source.Key}'.");

        var targetPage = await GetReplaceableTargetPageAsync(targetContext, sourcePage, cancellationToken);
        await using var transaction = await targetContext.Database.BeginTransactionAsync(cancellationToken);
        if (targetPage is not null)
        {
            await DeleteTargetPageAsync(targetContext, targetPage, cancellationToken);
        }
        await CopyPageAsync(targetContext, sourcePage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> CopyAllAsync(
        string sourceKey,
        string targetSourceKey,
        CancellationToken cancellationToken = default)
    {
        var (source, target) = ResolveDistinctSources(sourceKey, targetSourceKey);
        await using var sourceContext = CreateContext(source.Key);
        await using var targetContext = CreateContext(target.Key);
        await ContentStorageSchema.EnsureCurrentAsync(sourceContext, cancellationToken);
        await ContentStorageSchema.EnsureCurrentAsync(targetContext, cancellationToken);

        var sourcePages = await LoadAllPagesAsync(sourceContext, cancellationToken);
        if (sourcePages.Count == 0)
        {
            return 0;
        }

        var targetPages = new Dictionary<string, ContentPageRecord?>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourcePage in sourcePages)
        {
            targetPages[sourcePage.ContentKey] = await GetReplaceableTargetPageAsync(
                targetContext,
                sourcePage,
                cancellationToken);
        }

        await using var transaction = await targetContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var sourcePage in sourcePages.OrderBy(page => page.Id))
        {
            if (targetPages[sourcePage.ContentKey] is { } targetPage)
            {
                await DeleteTargetPageAsync(targetContext, targetPage, cancellationToken);
            }
            await CopyPageAsync(targetContext, sourcePage, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return sourcePages.Count;
    }

    private async Task CopyPageAsync(
        ContentDbContext targetContext,
        ContentPageRecord sourcePage,
        CancellationToken cancellationToken)
    {
        var targetPage = new ContentPageRecord
        {
            ContentKey = sourcePage.ContentKey,
            Slug = sourcePage.Slug
        };
        targetContext.Pages.Add(targetPage);
        await targetContext.SaveChangesAsync(cancellationToken);

        if (sourcePage.Assets.Count > 0)
        {
            targetContext.Assets.AddRange(sourcePage.Assets
                .OrderBy(asset => asset.Id)
                .Select(asset => new ContentAssetRecord
                {
                    AssetKey = asset.AssetKey,
                    PageId = targetPage.Id,
                    FileName = asset.FileName,
                    MediaType = asset.MediaType,
                    Length = asset.Length,
                    Sha256 = asset.Sha256,
                    CreatedUtc = NormalizeUtc(asset.CreatedUtc),
                    Data = asset.Data.ToArray()
                }));
            await targetContext.SaveChangesAsync(cancellationToken);
        }

        var revisionIdMap = new Dictionary<long, long>();
        var pending = sourcePage.Revisions
            .OrderBy(revision => revision.Id)
            .ToList();

        while (pending.Count > 0)
        {
            var copiedThisPass = 0;
            foreach (var sourceRevision in pending.ToList())
            {
                if (sourceRevision.ParentRevisionId.HasValue
                    && !revisionIdMap.ContainsKey(sourceRevision.ParentRevisionId.Value))
                {
                    continue;
                }

                var targetRevision = new ContentRevisionRecord
                {
                    PageId = targetPage.Id,
                    ParentRevisionId = sourceRevision.ParentRevisionId.HasValue
                        ? revisionIdMap[sourceRevision.ParentRevisionId.Value]
                        : null,
                    CreatedUtc = NormalizeUtc(sourceRevision.CreatedUtc),
                    BodyFormat = sourceRevision.BodyFormat,
                    MetadataJson = sourceRevision.MetadataJson,
                    Body = sourceRevision.Body
                };
                targetRevision.Tags.AddRange(sourceRevision.Tags.Select(tag => new ContentRevisionTagRecord
                {
                    Tag = tag.Tag
                }));
                targetRevision.Modes.AddRange(sourceRevision.Modes.Select(mode => new ContentRevisionModeRecord
                {
                    SiteMode = mode.SiteMode
                }));

                targetContext.Revisions.Add(targetRevision);
                await targetContext.SaveChangesAsync(cancellationToken);
                revisionIdMap[sourceRevision.Id] = targetRevision.Id;
                pending.Remove(sourceRevision);
                copiedThisPass++;
            }

            if (copiedThisPass == 0)
            {
                throw new InvalidOperationException(
                    $"Revision history for content page '{sourcePage.ContentKey}' contains an unresolved parent revision.");
            }
        }

        if (sourcePage.CurrentRevisionId.HasValue)
        {
            if (!revisionIdMap.TryGetValue(sourcePage.CurrentRevisionId.Value, out var currentRevisionId))
            {
                throw new InvalidOperationException(
                    $"Current revision for content page '{sourcePage.ContentKey}' was not copied.");
            }

            targetPage.CurrentRevisionId = currentRevisionId;
            await targetContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<ContentPageRecord?> GetReplaceableTargetPageAsync(
        ContentDbContext targetContext,
        ContentPageRecord sourcePage,
        CancellationToken cancellationToken)
    {
        var matches = await targetContext.Pages
            .Where(page => page.ContentKey == sourcePage.ContentKey || page.Slug == sourcePage.Slug)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1
            || matches.Count == 1
                && (!string.Equals(matches[0].ContentKey, sourcePage.ContentKey, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(matches[0].Slug, sourcePage.Slug, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The target source contains a different page using stable ID '{sourcePage.ContentKey}' or slug '{sourcePage.Slug}'.");
        }

        var targetPage = matches.SingleOrDefault();
        var assetKeys = sourcePage.Assets.Select(asset => asset.AssetKey).ToList();
        if (assetKeys.Count > 0
            && await targetContext.Assets.AnyAsync(
                asset => assetKeys.Contains(asset.AssetKey)
                    && (targetPage == null || asset.PageId != targetPage.Id),
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The target source contains another page using a content media key from this page.");
        }

        return targetPage;
    }

    private static async Task DeleteTargetPageAsync(
        ContentDbContext targetContext,
        ContentPageRecord targetPage,
        CancellationToken cancellationToken)
    {
        targetPage.CurrentRevisionId = null;
        await targetContext.SaveChangesAsync(cancellationToken);
        targetContext.Pages.Remove(targetPage);
        await targetContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ContentPageRecord?> LoadPageAsync(
        ContentDbContext context,
        string slug,
        CancellationToken cancellationToken) => await context.Pages
        .AsNoTracking()
        .Include(page => page.Assets)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Tags)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Modes)
        .SingleOrDefaultAsync(page => page.Slug == slug, cancellationToken);

    private static async Task<List<ContentPageRecord>> LoadAllPagesAsync(
        ContentDbContext context,
        CancellationToken cancellationToken) => await context.Pages
        .AsNoTracking()
        .Include(page => page.Assets)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Tags)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Modes)
        .ToListAsync(cancellationToken);

    private (ContentSourceDefinition Source, ContentSourceDefinition Target) ResolveDistinctSources(
        string sourceKey,
        string targetSourceKey)
    {
        var source = _sourceRegistry.GetSource(sourceKey);
        var target = _sourceRegistry.GetSource(targetSourceKey);

        if (string.Equals(source.Key, target.Key, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Choose a different target source.");
        }

        if (string.Equals(source.Provider, target.Provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.ConnectionString, target.ConnectionString, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Those source keys point to the same database.");
        }

        return (source, target);
    }

    private ContentDbContext CreateContext(string sourceKey)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>();
        _sourceRegistry.ConfigureDbContext(options, sourceKey);
        return new ContentDbContext(options.Options);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
