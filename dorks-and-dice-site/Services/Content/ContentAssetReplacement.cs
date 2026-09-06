using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Replaces the bytes behind an existing managed-media identity. Validation and media-type
/// sniffing are delegated to the normal upload path, while the stable asset key, canonical file
/// name, creation timestamp, page attachments, dependencies, and revision references remain
/// unchanged.
/// </summary>
public static class ContentAssetReplacement
{
    public static async Task<ContentAssetInfo> ReplaceAsync(
        IContentAssetService assets,
        IContentSourceRegistry sources,
        string sourceKey,
        string assetKey,
        string fileName,
        string mediaType,
        Stream stream,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(sources);

        var original = await assets.GetInfoFromSourceAsync(sourceKey, assetKey, cancellationToken)
            ?? throw new InvalidOperationException("Content media was not found in the selected source.");
        var preexistingAssetKeys = (await assets.GetForSourceAsync(sourceKey, cancellationToken))
            .Select(asset => asset.AssetKey)
            .ToHashSet(StringComparer.Ordinal);

        // UploadAsync is the canonical validation boundary for size limits, passive SVG checks,
        // file signatures, supported media types, and SHA-256 calculation. The uploaded identity
        // is used only as a validated staging record; the original identity is retained. Capture
        // the source inventory first so deduplication can never cause cleanup to delete an asset
        // that already existed before this replacement attempt.
        var staged = await assets.UploadAsync(
            sourceKey,
            fileName,
            mediaType,
            stream,
            declaredLength,
            cancellationToken);
        var stagedWasCreatedForReplacement = !preexistingAssetKeys.Contains(staged.AssetKey);

        if (!string.Equals(staged.MediaType, original.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            await RemoveReplacementStagingAssetAsync(
                sources,
                sourceKey,
                staged.AssetKey,
                original.AssetKey,
                stagedWasCreatedForReplacement,
                cancellationToken);
            throw new InvalidOperationException(
                $"Replacement media must keep the existing media type '{original.MediaType}'.");
        }

        if (string.Equals(staged.Sha256, original.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            await RemoveReplacementStagingAssetAsync(
                sources,
                sourceKey,
                staged.AssetKey,
                original.AssetKey,
                stagedWasCreatedForReplacement,
                cancellationToken);
            return original;
        }

        var source = sources.GetSource(sourceKey);
        await using var context = CreateContext(sources, source.Key);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var originalRecord = await context.Assets
            .SingleOrDefaultAsync(asset => asset.AssetKey == original.AssetKey, cancellationToken)
            ?? throw new InvalidOperationException("Content media disappeared before replacement completed.");
        var stagedRecord = await context.Assets
            .AsNoTracking()
            .SingleOrDefaultAsync(asset => asset.AssetKey == staged.AssetKey, cancellationToken)
            ?? throw new InvalidOperationException("Validated replacement media could not be reloaded.");

        originalRecord.MediaType = stagedRecord.MediaType;
        originalRecord.Length = stagedRecord.Length;
        originalRecord.Sha256 = stagedRecord.Sha256;
        originalRecord.Data = stagedRecord.Data.ToArray();
        await context.SaveChangesAsync(cancellationToken);

        if (stagedWasCreatedForReplacement
            && !string.Equals(stagedRecord.AssetKey, originalRecord.AssetKey, StringComparison.Ordinal))
        {
            var stagingIsAttached = await context.PageAssets
                .AnyAsync(link => link.AssetId == stagedRecord.Id, cancellationToken);
            if (!stagingIsAttached)
            {
                await context.Assets
                    .Where(asset => asset.Id == stagedRecord.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return await assets.GetInfoFromSourceAsync(source.Key, original.AssetKey, cancellationToken)
            ?? throw new InvalidOperationException("Replaced content media could not be reloaded.");
    }

    private static async Task RemoveReplacementStagingAssetAsync(
        IContentSourceRegistry sources,
        string sourceKey,
        string stagedAssetKey,
        string originalAssetKey,
        bool stagedWasCreatedForReplacement,
        CancellationToken cancellationToken)
    {
        if (!stagedWasCreatedForReplacement
            || string.Equals(stagedAssetKey, originalAssetKey, StringComparison.Ordinal))
        {
            return;
        }

        var source = sources.GetSource(sourceKey);
        await using var context = CreateContext(sources, source.Key);
        var staged = await context.Assets
            .SingleOrDefaultAsync(asset => asset.AssetKey == stagedAssetKey, cancellationToken);
        if (staged is null)
        {
            return;
        }

        var attached = await context.PageAssets
            .AnyAsync(link => link.AssetId == staged.Id, cancellationToken);
        if (!attached)
        {
            context.Assets.Remove(staged);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static ContentDbContext CreateContext(IContentSourceRegistry sources, string sourceKey)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>();
        sources.ConfigureDbContext(options, sourceKey);
        return new ContentDbContext(options.Options);
    }
}
