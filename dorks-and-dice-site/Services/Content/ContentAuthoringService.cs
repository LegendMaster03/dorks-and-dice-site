using System.Text.Json;
using System.Text.Json.Serialization;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentAuthoringService : IContentAuthoringService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = CreateMetadataJsonOptions();

    private readonly IContentSourceRegistry _sourceRegistry;

    public ContentAuthoringService(IContentSourceRegistry sourceRegistry)
    {
        _sourceRegistry = sourceRegistry;
    }

    public string DefaultSourceKey => _sourceRegistry.AuthoringSourceKey;

    public async Task<ContentAuthoringIndexViewModel> GetIndexAsync(
        string? sourceKey,
        CancellationToken cancellationToken = default)
    {
        var selectedSourceKey = ResolveSourceKey(sourceKey);
        await using var context = CreateContext(selectedSourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var repository = new DatabaseContentRepository(context);
        var items = await repository.GetAllAsync(cancellationToken);
        return new ContentAuthoringIndexViewModel
        {
            SelectedSourceKey = selectedSourceKey,
            Sources = GetSourceOptions(),
            MoveTargets = GetMoveTargets(selectedSourceKey),
            Items = items
                .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public async Task<ContentAuthoringEditViewModel?> GetEditAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        ContentInputValidator.ValidateKey("Slug", slug);
        await using var context = CreateContext(sourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var repository = new DatabaseContentRepository(context);
        var item = await repository.GetBySlugAsync(slug, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new ContentAuthoringEditViewModel
        {
            Document = ToDocument(item, sourceKey),
            Sources = GetSourceOptions(),
            History = await GetHistoryAsync(context, item.Id, cancellationToken)
        };
    }

    public ContentAuthoringEditViewModel GetNew(string? sourceKey)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        var metadata = new ContentItem
        {
            Title = "New content",
            Summary = "Describe this content.",
            LinkText = "Open details"
        };

        return new ContentAuthoringEditViewModel
        {
            Document = new ContentAuthoringDocument
            {
                IsNew = true,
                SourceKey = sourceKey,
                IsListed = true,
                MetadataJson = PrettyMetadata(ContentRecordMapper.SerializeMetadata(metadata)),
                TagsText = ContentTags.Article,
                VisibleModesText = SiteMode.Professional.ToString(),
                BodyFormat = "markdown",
                Body = "## Overview\n\nWrite the page body here."
            }
        };
    }

    public void PopulateOptions(ContentAuthoringEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Document.SourceKey))
        {
            model.Document.SourceKey = _sourceRegistry.AuthoringSourceKey;
        }

        model.Sources = GetSourceOptions();
    }

    public async Task<ContentItem> CreateAsync(
        ContentAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        document.SourceKey = ResolveSourceKey(document.SourceKey);
        await using var context = CreateContext(document.SourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var item = ParseAndValidate(document, requireExistingRevision: false);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        if (await context.Pages.AnyAsync(
                page => page.ContentKey == item.Id || page.Slug == item.Slug,
                cancellationToken))
        {
            throw new InvalidOperationException("A content page already uses that stable ID or slug.");
        }

        var page = new ContentPageRecord
        {
            ContentKey = item.Id,
            Slug = item.Slug
        };
        context.Pages.Add(page);
        await context.SaveChangesAsync(cancellationToken);

        var revision = CreateRevision(page.Id, parentRevisionId: null, item);
        context.Revisions.Add(revision);
        await context.SaveChangesAsync(cancellationToken);

        page.CurrentRevisionId = revision.Id;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        item.RevisionId = revision.Id;
        return item;
    }

    public async Task<ContentItem> SaveRevisionAsync(
        ContentAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        document.SourceKey = ResolveSourceKey(document.SourceKey);
        await using var context = CreateContext(document.SourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var item = ParseAndValidate(document, requireExistingRevision: true);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var page = await context.Pages
            .SingleOrDefaultAsync(candidate => candidate.ContentKey == item.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{item.Id}' no longer exists.");

        if (page.CurrentRevisionId != document.ExpectedRevisionId)
        {
            throw new ContentAuthoringConflictException(
                "This page changed after the editor was opened. Reload it before saving another revision.");
        }

        if (!string.Equals(page.Slug, item.Slug, StringComparison.OrdinalIgnoreCase)
            && await context.Pages.AnyAsync(candidate => candidate.Slug == item.Slug && candidate.Id != page.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Another content page already uses slug '{item.Slug}'.");
        }

        page.Slug = item.Slug;
        var revision = CreateRevision(page.Id, page.CurrentRevisionId, item);
        context.Revisions.Add(revision);
        await context.SaveChangesAsync(cancellationToken);

        page.CurrentRevisionId = revision.Id;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        item.RevisionId = revision.Id;
        return item;
    }

    public async Task MoveAsync(
        string sourceKey,
        string targetSourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ContentInputValidator.ValidateKey("Slug", slug);
        sourceKey = ResolveSourceKey(sourceKey);
        targetSourceKey = ResolveSourceKey(targetSourceKey);
        if (string.Equals(sourceKey, targetSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Choose a different target source.");
        }

        var source = _sourceRegistry.GetSource(sourceKey);
        var target = _sourceRegistry.GetSource(targetSourceKey);
        if (string.Equals(source.ConnectionString, target.ConnectionString, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Those source keys point to the same database.");
        }

        await using var sourceContext = CreateContext(sourceKey);
        await using var targetContext = CreateContext(targetSourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(sourceContext, cancellationToken);
        await ContentStorageSchema.EnsureCurrentAsync(targetContext, cancellationToken);

        var sourceRepository = new DatabaseContentRepository(sourceContext);
        var item = await sourceRepository.GetBySlugAsync(slug, cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{slug}' was not found in source '{sourceKey}'.");
        var sourcePage = await sourceContext.Pages
            .Include(page => page.Assets)
            .SingleAsync(page => page.ContentKey == item.Id, cancellationToken);

        if (await targetContext.Pages.AnyAsync(
                page => page.ContentKey == item.Id || page.Slug == item.Slug,
                cancellationToken))
        {
            throw new InvalidOperationException("The target source already contains a page with that stable ID or slug.");
        }

        var sourceAssetKeys = sourcePage.Assets.Select(asset => asset.AssetKey).ToList();
        if (sourceAssetKeys.Count > 0
            && await targetContext.Assets.AnyAsync(
                asset => sourceAssetKeys.Contains(asset.AssetKey),
                cancellationToken))
        {
            throw new InvalidOperationException("The target source already contains a content media key used by this page.");
        }

        await using var targetTransaction = await targetContext.Database.BeginTransactionAsync(cancellationToken);
        var targetPage = new ContentPageRecord
        {
            ContentKey = item.Id,
            Slug = item.Slug
        };
        targetContext.Pages.Add(targetPage);
        await targetContext.SaveChangesAsync(cancellationToken);

        if (sourcePage.Assets.Count > 0)
        {
            targetContext.Assets.AddRange(sourcePage.Assets.Select(asset => new ContentAssetRecord
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

        var revision = CreateRevision(targetPage.Id, parentRevisionId: null, item);
        targetContext.Revisions.Add(revision);
        await targetContext.SaveChangesAsync(cancellationToken);

        targetPage.CurrentRevisionId = revision.Id;
        await targetContext.SaveChangesAsync(cancellationToken);
        await targetTransaction.CommitAsync(cancellationToken);

        sourceContext.Pages.Remove(sourcePage);
        await sourceContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ContentRevisionSummary>> GetHistoryAsync(
        ContentDbContext context,
        string contentKey,
        CancellationToken cancellationToken)
    {
        var pageId = await context.Pages
            .Where(page => page.ContentKey == contentKey)
            .Select(page => (long?)page.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!pageId.HasValue)
        {
            return [];
        }

        return await context.Revisions
            .AsNoTracking()
            .Where(revision => revision.PageId == pageId.Value)
            .OrderByDescending(revision => revision.Id)
            .Select(revision => new ContentRevisionSummary
            {
                RevisionId = revision.Id,
                ParentRevisionId = revision.ParentRevisionId,
                CreatedUtc = revision.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    private static ContentRevisionRecord CreateRevision(long pageId, long? parentRevisionId, ContentItem item)
    {
        var revision = new ContentRevisionRecord
        {
            PageId = pageId,
            ParentRevisionId = parentRevisionId,
            CreatedUtc = DateTime.UtcNow,
            BodyFormat = item.BodyFormat,
            MetadataJson = ContentRecordMapper.SerializeMetadata(item),
            Body = item.Body
        };

        revision.Tags.AddRange(item.Tags.Select(tag => new ContentRevisionTagRecord { Tag = tag }));
        revision.Modes.AddRange(item.VisibleInModes.Select(mode => new ContentRevisionModeRecord { SiteMode = mode.ToString() }));
        return revision;
    }

    private static ContentAuthoringDocument ToDocument(ContentItem item, string sourceKey)
    {
        return new ContentAuthoringDocument
        {
            IsNew = false,
            SourceKey = sourceKey,
            Id = item.Id,
            Slug = item.Slug,
            ExpectedRevisionId = item.RevisionId,
            IsListed = item.IsListed,
            MetadataJson = PrettyMetadata(ContentRecordMapper.SerializeMetadata(item)),
            TagsText = string.Join(Environment.NewLine, item.Tags
                .Where(tag => !string.Equals(tag, ContentTags.Unlisted, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)),
            VisibleModesText = string.Join(Environment.NewLine, item.VisibleInModes.OrderBy(mode => mode.ToString())),
            BodyFormat = item.BodyFormat,
            Body = item.Body
        };
    }

    private static ContentItem ParseAndValidate(ContentAuthoringDocument document, bool requireExistingRevision)
    {
        ContentInputValidator.ValidateDocumentShape(document);

        if (requireExistingRevision && document.ExpectedRevisionId <= 0)
        {
            throw new InvalidOperationException("Expected revision ID is required when saving an existing page.");
        }

        ContentItem item;
        try
        {
            item = JsonSerializer.Deserialize<ContentItem>(document.MetadataJson, MetadataJsonOptions)
                ?? throw new JsonException("Metadata did not produce a content object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Metadata JSON is invalid: {ex.Message}", ex);
        }

        item.Id = document.Id;
        item.Slug = document.Slug;
        item.Tags = ContentInputValidator.ParseTags(document.TagsText);
        if (!document.IsListed)
        {
            item.Tags.Add(ContentTags.Unlisted);
        }
        item.VisibleInModes = ContentInputValidator.ParseModes(document.VisibleModesText);
        item.BodyFormat = document.BodyFormat.Trim().ToLowerInvariant();
        item.Body = document.Body;

        ContentInputValidator.ValidateItem(item);

        if (!item.Tags.Any(ContentTags.IsContext))
        {
            throw new InvalidOperationException("At least one context tag is required: project, experience, or article.");
        }

        if (item.VisibleInModes.Count == 0)
        {
            throw new InvalidOperationException("At least one visible site mode is required.");
        }

        return item;
    }

    private ContentDbContext CreateContext(string sourceKey)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>();
        _sourceRegistry.ConfigureDbContext(options, sourceKey);
        return new ContentDbContext(options.Options);
    }

    private string ResolveSourceKey(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return _sourceRegistry.AuthoringSourceKey;
        }

        return _sourceRegistry.GetSource(sourceKey).Key;
    }

    private List<ContentAuthoringSourceOption> GetSourceOptions() => _sourceRegistry
        .GetAllSources()
        .Select(source => new ContentAuthoringSourceOption
        {
            Key = source.Key,
            DisplayName = source.DisplayName
        })
        .ToList();

    private List<ContentAuthoringSourceOption> GetMoveTargets(string selectedSourceKey)
    {
        var selectedSource = _sourceRegistry.GetSource(selectedSourceKey);
        return _sourceRegistry
            .GetAllSources()
            .Where(source => !string.Equals(source.Key, selectedSourceKey, StringComparison.OrdinalIgnoreCase))
            .Where(source => !string.Equals(source.ConnectionString, selectedSource.ConnectionString, StringComparison.OrdinalIgnoreCase))
            .Select(source => new ContentAuthoringSourceOption
            {
                Key = source.Key,
                DisplayName = source.DisplayName
            })
            .ToList();
    }

    private static string PrettyMetadata(string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonSerializerOptions CreateMetadataJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
