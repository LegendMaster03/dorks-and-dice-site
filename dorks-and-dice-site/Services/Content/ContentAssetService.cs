using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public interface IContentAssetService
{
    Task<IReadOnlyList<ContentAssetInfo>> GetForSourceAsync(
        string sourceKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentAssetInfo>> SearchSourceAsync(
        string sourceKey, string query, int limit = 24,
        CancellationToken cancellationToken = default);
    Task<ContentAssetInfo?> GetInfoFromSourceAsync(
        string sourceKey, string assetKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentAssetInfo>> GetForPageAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default);

    Task<ContentAssetInfo> UploadAsync(
        string sourceKey,
        string fileName,
        string mediaType,
        Stream stream,
        long declaredLength,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetDependencyKeysAsync(string sourceKey, string slug, CancellationToken cancellationToken = default);
    Task AttachAsync(string sourceKey, string slug, string assetSourceKey, string assetKey, CancellationToken cancellationToken = default);
    Task DetachAsync(string sourceKey, string slug, string assetKey, CancellationToken cancellationToken = default);

    Task<ContentAssetFile?> GetForRequestAsync(
        string assetKey,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ContentAssetFile?> GetFromSourceAsync(
        string sourceKey,
        string assetKey,
        CancellationToken cancellationToken = default);
}

public sealed class ContentAssetService : IContentAssetService
{
    private static readonly IReadOnlyDictionary<string, string> AllowedMediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        };

    private readonly IContentSourceRegistry _sourceRegistry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentCatalogService _catalog;

    public ContentAssetService(
        IContentSourceRegistry sourceRegistry,
        IHttpContextAccessor httpContextAccessor,
        IContentCatalogService catalog)
    {
        _sourceRegistry = sourceRegistry;
        _httpContextAccessor = httpContextAccessor;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<ContentAssetInfo>> GetForSourceAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var assets = await context.Assets.AsNoTracking()
            .OrderBy(asset => asset.FileName)
            .ToListAsync(cancellationToken);
        return assets.Select(asset => ToInfo(
            asset.AssetKey, asset.FileName, asset.MediaType, asset.Length,
            asset.Sha256, NormalizeUtc(asset.CreatedUtc), sourceKey: source.Key)).ToList();
    }

    public async Task<IReadOnlyList<ContentAssetInfo>> SearchSourceAsync(
        string sourceKey,
        string query,
        int limit = 24,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        limit = Math.Clamp(limit, 1, 50);
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var assets = await context.Assets.AsNoTracking()
            .Where(asset => asset.FileName.Contains(query) || asset.AssetKey.Contains(query))
            .OrderBy(asset => asset.FileName)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return assets.Select(asset => ToInfo(
            asset.AssetKey, asset.FileName, asset.MediaType, asset.Length,
            asset.Sha256, NormalizeUtc(asset.CreatedUtc), sourceKey: source.Key)).ToList();
    }

    public async Task<ContentAssetInfo?> GetInfoFromSourceAsync(
        string sourceKey, string assetKey, CancellationToken cancellationToken = default)
    {
        ValidateAssetKey(assetKey);
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        var asset = await context.Assets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AssetKey == assetKey, cancellationToken);
        return asset is null ? null : ToInfo(
            asset.AssetKey, asset.FileName, asset.MediaType, asset.Length,
            asset.Sha256, NormalizeUtc(asset.CreatedUtc), sourceKey: source.Key);
    }

    public async Task<IReadOnlyList<ContentAssetInfo>> GetForPageAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);

        var pageId = await context.Pages
            .Where(page => page.Slug == slug)
            .Select(page => (long?)page.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{source.Key}'.");

        var assets = await context.PageAssets
            .AsNoTracking()
            .Where(link => link.PageId == pageId)
            .OrderBy(link => link.Asset!.FileName)
            .Select(link => new
            {
                link.Asset!.AssetKey,
                link.Asset.FileName,
                link.Asset.MediaType,
                link.Asset.Length,
                link.Asset.Sha256,
                link.Asset.CreatedUtc,
                link.Relationship
            })
            .ToListAsync(cancellationToken);

        return assets.Select(asset => ToInfo(
                asset.AssetKey,
                asset.FileName,
                asset.MediaType,
                asset.Length,
                asset.Sha256,
                NormalizeUtc(asset.CreatedUtc), asset.Relationship, source.Key))
            .ToList();
    }

    public async Task<ContentAssetInfo> UploadAsync(
        string sourceKey,
        string fileName,
        string mediaType,
        Stream stream,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        if (declaredLength <= 0)
        {
            throw new InvalidOperationException("Choose a non-empty image file.");
        }
        if (declaredLength > ContentInputPolicy.MaxAssetUploadBytes)
        {
            throw new InvalidOperationException(
                $"Image exceeds the {ContentInputPolicy.MaxAssetUploadBytes / (1024 * 1024)} MB upload limit.");
        }
        if (!AllowedMediaTypes.TryGetValue(mediaType, out var extension))
        {
            throw new InvalidOperationException("Supported image types are JPEG, PNG, WebP, and GIF.");
        }

        await using var buffer = new MemoryStream(capacity: (int)Math.Min(declaredLength, ContentInputPolicy.MaxAssetUploadBytes));
        await stream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0 || buffer.Length > ContentInputPolicy.MaxAssetUploadBytes)
        {
            throw new InvalidOperationException("Image upload size is invalid.");
        }

        var data = buffer.ToArray();
        ValidateImageSignature(mediaType, data);
        var normalizedFileName = NormalizeFileName(fileName, extension);
        var sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var source = _sourceRegistry.GetSource(sourceKey);

        await using var context = CreateContext(source.Key);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var existing = await context.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.Sha256 == sha256, cancellationToken);
        if (existing is not null)
        {
            return ToInfo(
                existing!.AssetKey,
                existing.FileName,
                existing.MediaType,
                existing.Length,
                existing.Sha256,
                NormalizeUtc(existing.CreatedUtc));
        }

        var record = new ContentAssetRecord
        {
            AssetKey = Guid.NewGuid().ToString("N"),
            FileName = normalizedFileName,
            MediaType = mediaType.ToLowerInvariant(),
            Length = data.LongLength,
            Sha256 = sha256,
            CreatedUtc = DateTime.UtcNow,
            Data = data
        };
        context.Assets.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return ToInfo(
            record.AssetKey,
            record.FileName,
            record.MediaType,
            record.Length,
            record.Sha256,
            record.CreatedUtc);
    }

    public async Task<IReadOnlySet<string>> GetDependencyKeysAsync(
        string sourceKey, string slug, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext(_sourceRegistry.GetSource(sourceKey).Key);
        var page = await context.Pages.Include(item => item.AssetLinks).ThenInclude(link => link.Asset)
            .Include(item => item.AssetDependencies)
            .SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{sourceKey}'.");
        return page.AssetLinks.Select(link => link.Asset!.AssetKey)
            .Concat(page.AssetDependencies.Select(link => link.AssetKey))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task AttachAsync(
        string sourceKey, string slug, string assetSourceKey, string assetKey,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        ValidateAssetKey(assetKey);
        var articleSource = _sourceRegistry.GetSource(sourceKey);
        var assetSource = _sourceRegistry.GetSource(assetSourceKey);
        if (!string.Equals(articleSource.Key, assetSource.Key, StringComparison.OrdinalIgnoreCase)
            && !_sourceRegistry.IsGlobalSource(assetSource.Key))
        {
            throw new InvalidOperationException("Articles may only depend on media in their own database or a Global database.");
        }
        await using var context = CreateContext(articleSource.Key);
        var pageId = await context.Pages.Where(page => page.Slug == slug).Select(page => (long?)page.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{sourceKey}'.");
        if (string.Equals(articleSource.Key, assetSource.Key, StringComparison.OrdinalIgnoreCase))
        {
            var assetId = await context.Assets.Where(asset => asset.AssetKey == assetKey).Select(asset => (long?)asset.Id)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Content media was not found in the selected source.");
            if (!await context.PageAssets.AnyAsync(link => link.PageId == pageId && link.AssetId == assetId, cancellationToken))
            {
                context.PageAssets.Add(new ContentPageAssetRecord
                {
                    PageId = pageId, AssetId = assetId, Relationship = ContentAssetRelationships.Attached
                });
            }
        }
        else
        {
            await using var globalContext = CreateContext(assetSource.Key);
            if (!await globalContext.Assets.AnyAsync(asset => asset.AssetKey == assetKey, cancellationToken))
                throw new InvalidOperationException("Content media was not found in the Global source.");
            if (!await context.PageAssetDependencies.AnyAsync(
                    link => link.PageId == pageId && link.AssetSourceKey == assetSource.Key
                        && link.AssetKey == assetKey, cancellationToken))
                context.PageAssetDependencies.Add(new ContentPageAssetDependencyRecord
                {
                    PageId = pageId, AssetSourceKey = assetSource.Key, AssetKey = assetKey
                });
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DetachAsync(string sourceKey, string slug, string assetKey, CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        ValidateAssetKey(assetKey);
        await using var context = CreateContext(_sourceRegistry.GetSource(sourceKey).Key);
        var link = await context.PageAssets.SingleOrDefaultAsync(
            candidate => candidate.Page!.Slug == slug && candidate.Asset!.AssetKey == assetKey, cancellationToken)
            ;
        var externalLink = link is null
            ? await context.PageAssetDependencies.SingleOrDefaultAsync(
                candidate => candidate.Page!.Slug == slug && candidate.AssetKey == assetKey, cancellationToken)
            : null;
        if (link is null && externalLink is null)
            throw new InvalidOperationException("That media dependency does not exist.");
        var isReferenced = await context.Pages
            .Where(page => page.Id == (link != null ? link.PageId : externalLink!.PageId) && page.CurrentRevisionId != null)
            .AnyAsync(page => context.RevisionAssets.Any(reference =>
                reference.RevisionId == page.CurrentRevisionId && reference.AssetKey == assetKey), cancellationToken);
        if (isReferenced)
        {
            throw new InvalidOperationException(
                "Remove this media reference from the article and save a revision before removing the dependency.");
        }
        if (link is not null) context.PageAssets.Remove(link);
        else context.PageAssetDependencies.Remove(externalLink!);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContentAssetFile?> GetForRequestAsync(
        string assetKey,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetKey(assetKey);
        var modeContext = GetSiteModeContext();
        var pageSources = _sourceRegistry.GetSourcesForContext(modeContext).ToList();
        var assetSources = pageSources.ToList();

        foreach (var globalSource in _sourceRegistry.GetGlobalSources())
        {
            if (!assetSources.Any(source => string.Equals(source.Key, globalSource.Key, StringComparison.OrdinalIgnoreCase)))
            {
                assetSources.Add(globalSource);
            }
        }

        for (var index = assetSources.Count - 1; index >= 0; index--)
        {
            var source = assetSources[index];
            await using var context = CreateContext(source.Key);
            var asset = await context.Assets
                .AsNoTracking()
                .Where(candidate => candidate.AssetKey == assetKey)
                .Select(candidate => new
                {
                    candidate.FileName,
                    candidate.MediaType,
                    candidate.Sha256
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (asset is null)
            {
                continue;
            }
            if (!string.Equals(asset.FileName, fileName, StringComparison.Ordinal))
            {
                return null;
            }

            if (!await IsReferencedByVisiblePageAsync(
                    source.Key,
                    assetKey,
                    pageSources,
                    modeContext,
                    cancellationToken))
            {
                return null;
            }

            var data = await context.Assets
                .AsNoTracking()
                .Where(candidate => candidate.AssetKey == assetKey)
                .Select(candidate => candidate.Data)
                .SingleOrDefaultAsync(cancellationToken);
            if (data is null)
            {
                return null;
            }

            return new ContentAssetFile
            {
                FileName = asset.FileName,
                MediaType = asset.MediaType,
                Sha256 = asset.Sha256,
                Data = data
            };
        }

        return null;
    }

    private async Task<bool> IsReferencedByVisiblePageAsync(
        string assetSourceKey,
        string assetKey,
        IReadOnlyList<ContentSourceDefinition> pageSources,
        SiteModeContext modeContext,
        CancellationToken cancellationToken)
    {
        var visibleModeId = modeContext.ActiveModeId;
        var legacyVisibleMode = modeContext.ActiveMode?.LegacyMode?.ToString();
        var isSyntheticMode = modeContext.SyntheticMode is not null;

        for (var pageSourceIndex = 0; pageSourceIndex < pageSources.Count; pageSourceIndex++)
        {
            var pageSource = pageSources[pageSourceIndex];
            await using var context = CreateContext(pageSource.Key);
            var isAssetInPageSource = string.Equals(
                pageSource.Key,
                assetSourceKey,
                StringComparison.OrdinalIgnoreCase);
            var referencingPages = await context.Pages
                .AsNoTracking()
                .Where(page => page.CurrentRevisionId != null
                    && context.RevisionAssets.Any(reference =>
                        reference.RevisionId == page.CurrentRevisionId
                        && reference.AssetKey == assetKey)
                    && (isAssetInPageSource && context.PageAssets.Any(link =>
                            link.PageId == page.Id
                            && link.Asset!.AssetKey == assetKey)
                        || context.PageAssetDependencies.Any(dependency =>
                            dependency.PageId == page.Id
                            && dependency.AssetSourceKey == assetSourceKey
                            && dependency.AssetKey == assetKey))
                    && (isSyntheticMode
                        || (visibleModeId != null
                            && context.RevisionModes.Any(mode =>
                                mode.RevisionId == page.CurrentRevisionId
                                && (mode.SiteMode == visibleModeId
                                    || (legacyVisibleMode != null && mode.SiteMode == legacyVisibleMode))))))
                .Select(page => new { page.ContentKey, page.Slug })
                .ToListAsync(cancellationToken);

            foreach (var page in referencingPages)
            {
                if (isSyntheticMode)
                {
                    var visiblePage = await _catalog.GetForDetailAsync(
                        page.Slug,
                        modeContext,
                        cancellationToken);
                    if (visiblePage is not null
                        && string.Equals(visiblePage.Id, page.ContentKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(visiblePage.SourceKey, pageSource.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    continue;
                }

                if (await IsEffectivePageAsync(
                        pageSourceIndex,
                        page.ContentKey,
                        page.Slug,
                        pageSources,
                        cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> IsEffectivePageAsync(
        int pageSourceIndex,
        string contentKey,
        string slug,
        IReadOnlyList<ContentSourceDefinition> pageSources,
        CancellationToken cancellationToken)
    {
        for (var index = pageSourceIndex + 1; index < pageSources.Count; index++)
        {
            await using var context = CreateContext(pageSources[index].Key);
            var isShadowed = await context.Pages
                .AsNoTracking()
                .AnyAsync(page => page.ContentKey == contentKey || page.Slug == slug, cancellationToken);
            if (isShadowed)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<ContentAssetFile?> GetFromSourceAsync(
        string sourceKey,
        string assetKey,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetKey(assetKey);
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        var asset = await context.Assets
            .AsNoTracking()
            .Where(candidate => candidate.AssetKey == assetKey)
            .Select(candidate => new ContentAssetFile
            {
                FileName = candidate.FileName,
                MediaType = candidate.MediaType,
                Sha256 = candidate.Sha256,
                Data = candidate.Data
            })
            .SingleOrDefaultAsync(cancellationToken);
        return asset;
    }

    private ContentDbContext CreateContext(string sourceKey)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>();
        _sourceRegistry.ConfigureDbContext(options, sourceKey);
        return new ContentDbContext(options.Options);
    }

    private SiteModeContext GetSiteModeContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items[SiteModeContext.HttpContextItemKey] is SiteModeContext siteModeContext)
        {
            return siteModeContext;
        }

        return new SiteModeContext
        {
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true
        };
    }

    private static ContentAssetInfo ToInfo(
        string assetKey,
        string fileName,
        string mediaType,
        long length,
        string sha256,
        DateTime createdUtc,
        string? relationship = null,
        string sourceKey = "")
    {
        var url = $"/content/media/{assetKey}/{fileName}";
        return new ContentAssetInfo
        {
            AssetKey = assetKey,
            FileName = fileName,
            MediaType = mediaType,
            Length = length,
            Sha256 = sha256,
            CreatedUtc = createdUtc,
            Url = url,
            MarkdownReference = $"![Alt text]({url})",
            Relationship = relationship,
            SourceKey = sourceKey
        };
    }

    private static void ValidateAssetKey(string assetKey)
    {
        if (!Guid.TryParseExact(assetKey, "N", out _))
        {
            throw new InvalidOperationException("Content media key is invalid.");
        }
    }

    private static string NormalizeFileName(string fileName, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName));
        var normalized = new StringBuilder();
        foreach (var character in baseName)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            {
                normalized.Append(character);
            }
            else if (normalized.Length > 0 && normalized[^1] != '-')
            {
                normalized.Append('-');
            }
        }

        var safeBaseName = normalized.ToString().Trim('-', '_');
        if (safeBaseName.Length == 0)
        {
            safeBaseName = "image";
        }

        var maxBaseLength = ContentInputPolicy.MaxAssetFileNameLength - extension.Length;
        if (safeBaseName.Length > maxBaseLength)
        {
            safeBaseName = safeBaseName[..maxBaseLength];
        }

        return safeBaseName + extension;
    }

    private static void ValidateImageSignature(string mediaType, byte[] data)
    {
        var span = data.AsSpan();
        var valid = mediaType.ToLowerInvariant() switch
        {
            "image/jpeg" => span.Length >= 3 && span[0] == 0xff && span[1] == 0xd8 && span[2] == 0xff,
            "image/png" => span.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/gif" => span.StartsWith("GIF87a"u8) || span.StartsWith("GIF89a"u8),
            "image/webp" => span.Length >= 12 && span[..4].SequenceEqual("RIFF"u8) && span.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException("The uploaded file does not match its declared image type.");
        }
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
