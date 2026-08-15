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
            AuthoringSourceKey = _sourceRegistry.AuthoringSourceKey,
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
                VisibleModesSelection = [SiteMode.Professional.ToString()],
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
        if (ContentAssetReferenceParser.FindAssetKeys(item.Body, ContentRecordMapper.SerializeMetadata(item)).Count > 0)
        {
            throw new InvalidOperationException(
                "Create the page first, attach its media dependencies, and then add media references in a revision.");
        }
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

        await ValidateAssetDependenciesAsync(context, page.Id, item, cancellationToken);

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
        ValidatePromotionSources(sourceKey, targetSourceKey);

        var transfer = new ContentSourceTransferService(_sourceRegistry);
        await transfer.CopyAsync(sourceKey, targetSourceKey, slug, cancellationToken);

        await using var sourceContext = CreateContext(sourceKey);
        var sourcePage = await sourceContext.Pages
            .Include(page => page.AssetLinks)
            .SingleAsync(page => page.Slug == slug, cancellationToken);
        await DeleteSourcePageAsync(sourceContext, sourcePage, cancellationToken);
    }

    public async Task<int> MoveAllAsync(
        string sourceKey,
        string targetSourceKey,
        CancellationToken cancellationToken = default)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        targetSourceKey = ResolveSourceKey(targetSourceKey);
        ValidatePromotionSources(sourceKey, targetSourceKey);

        var transfer = new ContentSourceTransferService(_sourceRegistry);
        var copiedCount = await transfer.CopyAllAsync(sourceKey, targetSourceKey, cancellationToken);
        if (copiedCount == 0) return 0;

        await using var sourceContext = CreateContext(sourceKey);
        var sourcePages = await sourceContext.Pages
            .Include(page => page.AssetLinks)
            .OrderBy(page => page.Id)
            .ToListAsync(cancellationToken);
        await using var transaction = await sourceContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var sourcePage in sourcePages)
        {
            await DeleteSourcePageAsync(sourceContext, sourcePage, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return copiedCount;
    }

    private static async Task DeleteSourcePageAsync(
        ContentDbContext sourceContext,
        ContentPageRecord sourcePage,
        CancellationToken cancellationToken)
    {
        var assetIds = sourcePage.AssetLinks.Select(link => link.AssetId).ToList();
        sourcePage.CurrentRevisionId = null;
        await sourceContext.SaveChangesAsync(cancellationToken);
        var revisionIds = await sourceContext.Revisions
            .Where(revision => revision.PageId == sourcePage.Id)
            .OrderByDescending(revision => revision.Id)
            .Select(revision => revision.Id)
            .ToListAsync(cancellationToken);
        foreach (var revisionId in revisionIds)
        {
            await sourceContext.Revisions
                .Where(revision => revision.Id == revisionId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        await sourceContext.Pages
            .Where(page => page.Id == sourcePage.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await sourceContext.Assets
            .Where(asset => assetIds.Contains(asset.Id) && !asset.PageLinks.Any())
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void ValidatePromotionSources(string sourceKey, string targetSourceKey)
    {
        if (!string.Equals(sourceKey, _sourceRegistry.AuthoringSourceKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Content promotion must begin in the configured authoring workspace.");
        if (!_sourceRegistry.IsGlobalSource(targetSourceKey))
            throw new InvalidOperationException("Content may only be promoted to a configured Global source.");
        if (string.Equals(sourceKey, targetSourceKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a different target source.");
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
        revision.AssetReferences.AddRange(ContentAssetReferenceParser
            .FindAssetKeys(revision.Body, revision.MetadataJson)
            .Select(assetKey => new ContentRevisionAssetRecord
            {
                AssetKey = assetKey,
                Relationship = ContentAssetRelationships.Embedded
            }));
        return revision;
    }

    private static async Task ValidateAssetDependenciesAsync(
        ContentDbContext context,
        long pageId,
        ContentItem item,
        CancellationToken cancellationToken)
    {
        var referencedKeys = ContentAssetReferenceParser
            .FindAssetKeys(item.Body, ContentRecordMapper.SerializeMetadata(item));
        if (referencedKeys.Count == 0) return;

        var linkedKeys = (await context.PageAssets
                .Where(link => link.PageId == pageId && referencedKeys.Contains(link.Asset!.AssetKey))
                .Select(link => link.Asset!.AssetKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var key in await context.PageAssetDependencies
                     .Where(link => link.PageId == pageId && referencedKeys.Contains(link.AssetKey))
                     .Select(link => link.AssetKey)
                     .ToListAsync(cancellationToken))
        {
            linkedKeys.Add(key);
        }
        var missing = referencedKeys.Where(key => !linkedKeys.Contains(key)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Attach every referenced media item to this page before saving. Missing dependencies: {string.Join(", ", missing)}.");
        }
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
            VisibleModesSelection = item.VisibleInModes
                .OrderBy(mode => mode.ToString())
                .Select(mode => mode.ToString())
                .ToList(),
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
        var selectedModes = document.VisibleModesSelection.Count > 0
            ? string.Join(Environment.NewLine, document.VisibleModesSelection)
            : document.VisibleModesText;
        item.VisibleInModes = ContentInputValidator.ParseModes(selectedModes);
        if (item.VisibleInModes.Any(mode => mode is SiteMode.Development or SiteMode.Unassigned))
        {
            throw new InvalidOperationException(
                "Development and Unassigned are runtime modes and cannot be selected for article visibility.");
        }
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
        if (!string.Equals(selectedSourceKey, _sourceRegistry.AuthoringSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var selectedSource = _sourceRegistry.GetSource(selectedSourceKey);
        return _sourceRegistry
            .GetGlobalSources()
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
