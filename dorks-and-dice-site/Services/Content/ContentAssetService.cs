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
    Task<IReadOnlyList<ContentAssetInfo>> GetForPageAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default);

    Task<ContentAssetInfo> UploadAsync(
        string sourceKey,
        string slug,
        string fileName,
        string mediaType,
        Stream stream,
        long declaredLength,
        CancellationToken cancellationToken = default);

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

    public async Task<IReadOnlyList<ContentAssetInfo>> GetForPageAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        var source = _sourceRegistry.GetSource(sourceKey);
        await using var context = CreateContext(source.Key);
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var pageId = await context.Pages
            .Where(page => page.Slug == slug)
            .Select(page => (long?)page.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{source.Key}'.");

        var assets = await context.Assets
            .AsNoTracking()
            .Where(asset => asset.PageId == pageId)
            .OrderBy(asset => asset.FileName)
            .Select(asset => new
            {
                asset.AssetKey,
                asset.FileName,
                asset.MediaType,
                asset.Length,
                asset.Sha256,
                asset.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return assets.Select(asset => ToInfo(
                asset.AssetKey,
                asset.FileName,
                asset.MediaType,
                asset.Length,
                asset.Sha256,
                NormalizeUtc(asset.CreatedUtc)))
            .ToList();
    }

    public async Task<ContentAssetInfo> UploadAsync(
        string sourceKey,
        string slug,
        string fileName,
        string mediaType,
        Stream stream,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
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
        await context.Database.EnsureCreatedAsync(cancellationToken);
        var page = await context.Pages
            .SingleOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{source.Key}'.");

        var existing = await context.Assets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                asset => asset.PageId == page.Id && asset.Sha256 == sha256,
                cancellationToken);
        if (existing is not null)
        {
            return ToInfo(
                existing.AssetKey,
                existing.FileName,
                existing.MediaType,
                existing.Length,
                existing.Sha256,
                NormalizeUtc(existing.CreatedUtc));
        }

        var record = new ContentAssetRecord
        {
            AssetKey = Guid.NewGuid().ToString("N"),
            PageId = page.Id,
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

    public async Task<ContentAssetFile?> GetForRequestAsync(
        string assetKey,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetKey(assetKey);
        var modeContext = GetSiteModeContext();
        var sources = modeContext.IsDevelopmentPreview && modeContext.HasContentSourceOverride
            ? _sourceRegistry.GetSourcesByKeys(modeContext.EnabledContentSources)
            : _sourceRegistry.GetDefaultSources(modeContext.SiteMode);

        if (sources.Count == 0 && modeContext.IsDevelopmentPreview)
        {
            sources = [_sourceRegistry.GetSource(_sourceRegistry.AuthoringSourceKey)];
        }

        for (var index = sources.Count - 1; index >= 0; index--)
        {
            var source = sources[index];
            await using var context = CreateContext(source.Key);
            var asset = await context.Assets
                .AsNoTracking()
                .Where(candidate => candidate.AssetKey == assetKey)
                .Select(candidate => new
                {
                    candidate.FileName,
                    candidate.MediaType,
                    candidate.Sha256,
                    candidate.Data,
                    PageKey = candidate.Page!.ContentKey,
                    PageSlug = candidate.Page.Slug
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

            var visiblePage = await _catalog.GetForDetailAsync(
                asset.PageSlug,
                modeContext.SiteMode,
                modeContext.IsDevelopmentPreview,
                cancellationToken);
            if (visiblePage is null
                || !string.Equals(visiblePage.Id, asset.PageKey, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new ContentAssetFile
            {
                FileName = asset.FileName,
                MediaType = asset.MediaType,
                Sha256 = asset.Sha256,
                Data = asset.Data
            };
        }

        return null;
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
            SiteMode = SiteMode.Development,
            IsDevelopmentPreview = true
        };
    }

    private static ContentAssetInfo ToInfo(
        string assetKey,
        string fileName,
        string mediaType,
        long length,
        string sha256,
        DateTime createdUtc)
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
            MarkdownReference = $"![Alt text]({url})"
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
