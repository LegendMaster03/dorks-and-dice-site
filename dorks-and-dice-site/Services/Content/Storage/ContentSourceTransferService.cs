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
        await EnsureDependenciesAvailableAsync(targetContext, target.Key, sourcePage, cancellationToken);
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
            await EnsureDependenciesAvailableAsync(targetContext, target.Key, sourcePage, cancellationToken);
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

        if (sourcePage.AssetLinks.Count > 0)
        {
            targetContext.Assets.AddRange(sourcePage.AssetLinks
                .OrderBy(link => link.AssetId)
                .Select(link => new ContentAssetRecord
                {
                    AssetKey = link.Asset!.AssetKey,
                    FileName = link.Asset.FileName,
                    MediaType = link.Asset.MediaType,
                    Length = link.Asset.Length,
                    Sha256 = link.Asset.Sha256,
                    CreatedUtc = NormalizeUtc(link.Asset.CreatedUtc),
                    Data = link.Asset.Data.ToArray(),
                    PageLinks =
                    [
                        new ContentPageAssetRecord
                        {
                            PageId = targetPage.Id,
                            Relationship = link.Relationship
                        }
                    ]
                }));
            await targetContext.SaveChangesAsync(cancellationToken);
        }

        targetPage.AssetDependencies.AddRange(sourcePage.AssetDependencies.Select(link =>
            new ContentPageAssetDependencyRecord
            {
                AssetSourceKey = link.AssetSourceKey,
                AssetKey = link.AssetKey
            }));
        if (targetPage.AssetDependencies.Count > 0)
            await targetContext.SaveChangesAsync(cancellationToken);

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
                targetRevision.AssetReferences.AddRange(sourceRevision.AssetReferences.Select(reference =>
                    new ContentRevisionAssetRecord
                    {
                        AssetKey = reference.AssetKey,
                        Relationship = reference.Relationship
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
        var assetKeys = sourcePage.AssetLinks.Select(link => link.Asset!.AssetKey).ToList();
        if (assetKeys.Count > 0
            && await targetContext.Assets.AnyAsync(
                asset => assetKeys.Contains(asset.AssetKey)
                    && (targetPage == null || !asset.PageLinks.Any(link => link.PageId == targetPage.Id)),
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

        var revisionIds = await targetContext.Revisions
            .Where(revision => revision.PageId == targetPage.Id)
            .OrderByDescending(revision => revision.Id)
            .Select(revision => revision.Id)
            .ToListAsync(cancellationToken);
        foreach (var revisionId in revisionIds)
        {
            await targetContext.Revisions
                .Where(revision => revision.Id == revisionId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var linkedAssetIds = await targetContext.PageAssets
            .Where(link => link.PageId == targetPage.Id)
            .Select(link => link.AssetId)
            .ToListAsync(cancellationToken);
        await targetContext.Pages
            .Where(page => page.Id == targetPage.Id)
            .ExecuteDeleteAsync(cancellationToken);
        targetContext.Entry(targetPage).State = EntityState.Detached;
        await targetContext.Assets
            .Where(asset => linkedAssetIds.Contains(asset.Id) && !asset.PageLinks.Any())
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<ContentPageRecord?> LoadPageAsync(
        ContentDbContext context,
        string slug,
        CancellationToken cancellationToken) => await context.Pages
        .AsNoTracking()
        .Include(page => page.AssetLinks)
            .ThenInclude(link => link.Asset)
        .Include(page => page.AssetDependencies)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Tags)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Modes)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.AssetReferences)
        .SingleOrDefaultAsync(page => page.Slug == slug, cancellationToken);

    private static async Task<List<ContentPageRecord>> LoadAllPagesAsync(
        ContentDbContext context,
        CancellationToken cancellationToken) => await context.Pages
        .AsNoTracking()
        .Include(page => page.AssetLinks)
            .ThenInclude(link => link.Asset)
        .Include(page => page.AssetDependencies)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Tags)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.Modes)
        .Include(page => page.Revisions)
            .ThenInclude(revision => revision.AssetReferences)
        .ToListAsync(cancellationToken);

    private async Task EnsureDependenciesAvailableAsync(
        ContentDbContext targetContext,
        string targetSourceKey,
        ContentPageRecord sourcePage,
        CancellationToken cancellationToken)
    {
        var bundledKeys = sourcePage.AssetLinks
            .Select(link => link.Asset!.AssetKey)
            .ToHashSet(StringComparer.Ordinal);
        var requiredKeys = sourcePage.Revisions
            .SelectMany(revision => revision.AssetReferences)
            .Select(reference => reference.AssetKey)
            .Where(assetKey => !bundledKeys.Contains(assetKey))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requiredKeys.Count == 0) return;

        var available = (await targetContext.Assets
                .Where(asset => requiredKeys.Contains(asset.AssetKey))
                .Select(asset => asset.AssetKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var globalSource in _sourceRegistry.GetGlobalSources()
                     .Where(source => !string.Equals(source.Key, targetSourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            await using var globalContext = CreateContext(globalSource.Key);
            foreach (var key in await globalContext.Assets
                         .Where(asset => requiredKeys.Contains(asset.AssetKey))
                         .Select(asset => asset.AssetKey)
                         .ToListAsync(cancellationToken))
            {
                available.Add(key);
            }
        }

        var missing = requiredKeys.Where(key => !available.Contains(key)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Content page '{sourcePage.ContentKey}' references media that is not bundled with the page or available from a Global source: {string.Join(", ", missing)}.");
        }
    }

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
