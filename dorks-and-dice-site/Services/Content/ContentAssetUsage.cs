using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Enriches authoring media records with every configured page dependency on the stable asset key.
/// Same-source ownership/attachments come from content_page_asset; cross-source dependencies come
/// from content_page_asset_dependency. This is authoring metadata, not public visibility filtering.
/// </summary>
public static class ContentAssetUsage
{
    public static async Task PopulateAsync(
        IContentSourceRegistry sourceRegistry,
        IEnumerable<ContentAssetInfo> assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceRegistry);
        ArgumentNullException.ThrowIfNull(assets);

        var materialized = assets.ToList();
        foreach (var sourceGroup in materialized
                     .Where(asset => !string.IsNullOrWhiteSpace(asset.SourceKey))
                     .GroupBy(asset => asset.SourceKey, StringComparer.OrdinalIgnoreCase))
        {
            var assetKeys = sourceGroup
                .Select(asset => asset.AssetKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var usages = await GetForAssetsAsync(
                sourceRegistry,
                sourceGroup.Key,
                assetKeys,
                cancellationToken);

            foreach (var asset in sourceGroup)
            {
                asset.PageDependencies = usages.TryGetValue(asset.AssetKey, out var assetUsages)
                    ? assetUsages
                    : [];
            }
        }
    }

    private static async Task<Dictionary<string, List<ContentAssetUsageInfo>>> GetForAssetsAsync(
        IContentSourceRegistry sourceRegistry,
        string assetSourceKey,
        IReadOnlyCollection<string> assetKeys,
        CancellationToken cancellationToken)
    {
        var assetSource = sourceRegistry.GetSource(assetSourceKey);
        var result = assetKeys.ToDictionary(
            key => key,
            _ => new List<ContentAssetUsageInfo>(),
            StringComparer.Ordinal);

        if (assetKeys.Count == 0)
        {
            return result;
        }

        var keys = assetKeys.ToArray();
        foreach (var pageSource in sourceRegistry.GetAllSources())
        {
            await using var context = CreateContext(sourceRegistry, pageSource.Key);
            await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);

            if (string.Equals(pageSource.Key, assetSource.Key, StringComparison.OrdinalIgnoreCase))
            {
                var localLinks = await context.PageAssets
                    .AsNoTracking()
                    .Where(link => keys.Contains(link.Asset!.AssetKey))
                    .Select(link => new
                    {
                        link.Asset!.AssetKey,
                        link.Page!.Slug,
                        link.Relationship
                    })
                    .ToListAsync(cancellationToken);

                foreach (var link in localLinks)
                {
                    result[link.AssetKey].Add(new ContentAssetUsageInfo
                    {
                        PageSourceKey = pageSource.Key,
                        Slug = link.Slug,
                        Relationship = link.Relationship
                    });
                }
            }

            var externalDependencies = await context.PageAssetDependencies
                .AsNoTracking()
                .Where(link => link.AssetSourceKey == assetSource.Key && keys.Contains(link.AssetKey))
                .Select(link => new
                {
                    link.AssetKey,
                    link.Page!.Slug
                })
                .ToListAsync(cancellationToken);

            foreach (var dependency in externalDependencies)
            {
                result[dependency.AssetKey].Add(new ContentAssetUsageInfo
                {
                    PageSourceKey = pageSource.Key,
                    Slug = dependency.Slug,
                    Relationship = "dependency"
                });
            }
        }

        foreach (var key in result.Keys.ToList())
        {
            result[key] = result[key]
                .GroupBy(
                    usage => $"{usage.PageSourceKey}\u001f{usage.Slug}\u001f{usage.Relationship}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(usage => usage.PageSourceKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.Slug, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.Relationship, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result;
    }

    private static ContentDbContext CreateContext(IContentSourceRegistry sourceRegistry, string sourceKey)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>();
        sourceRegistry.ConfigureDbContext(options, sourceKey);
        return new ContentDbContext(options.Options);
    }
}
