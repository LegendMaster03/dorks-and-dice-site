using System.Text.Json;
using System.Text.Json.Serialization;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentAuthoringService : IContentAuthoringService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = CreateMetadataJsonOptions();

    private readonly IContentSourceRegistry _sourceRegistry;
    private readonly ISiteModeRegistry _siteModeRegistry;

    // Temporary constructor bridge for unit fixtures and non-DI callers that predate the
    // composed site-mode registry. Runtime DI uses the two-argument constructor below.
    public ContentAuthoringService(IContentSourceRegistry sourceRegistry)
        : this(
            sourceRegistry,
            new SiteModeRegistry(new DeploymentSiteModeRegistrationSource().GetDefinitions()))
    {
    }

    public ContentAuthoringService(
        IContentSourceRegistry sourceRegistry,
        ISiteModeRegistry siteModeRegistry)
    {
        _sourceRegistry = sourceRegistry;
        _siteModeRegistry = siteModeRegistry;
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
            Modes = GetModeOptions(),
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
        var defaultModeId = _siteModeRegistry.All[0].Id;

        return new ContentAuthoringEditViewModel
        {
            Document = new ContentAuthoringDocument
            {
                IsNew = true,
                SourceKey = sourceKey,
                IsListed = true,
                MetadataJson = PrettyMetadata(ContentRecordMapper.SerializeMetadata(metadata)),
                TagsText = ContentTags.Article,
                VisibleModesText = defaultModeId,
                VisibleModesSelection = [defaultModeId],
                BodyFormat = "markdown",
                Body = "## Overview\n\nWrite the page body here."
            },
            Modes = GetModeOptions()
        };
    }

    public void PopulateOptions(ContentAuthoringEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Document.SourceKey))
        {
            model.Document.SourceKey = _sourceRegistry.AuthoringSourceKey;
        }

        model.Sources = GetSourceOptions();
        model.Modes = GetModeOptions();
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
        if (await context.Redirects.AnyAsync(
                redirect => redirect.Slug == item.Slug,
                cancellationToken))
        {
            throw new InvalidOperationException("A content redirect already uses that slug.");
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
            .Include(existing => existing.CurrentRevision)
            .SingleOrDefaultAsync(existing => existing.ContentKey == item.Id, cancellationToken)
            ?? throw new InvalidOperationException("The content page no longer exists.");
        if (page.CurrentRevisionId != document.ExpectedRevisionId)
        {
            throw new ContentAuthoringConflictException(
                "This page changed after the editor was opened. Reload it before saving again.");
        }
        if (await context.Pages.AnyAsync(
                existing => existing.Id != page.Id && existing.Slug == item.Slug,
                cancellationToken))
        {
            throw new InvalidOperationException("Another content page already uses that slug.");
        }
        if (await context.Redirects.AnyAsync(
                redirect => redirect.ContentKey != item.Id && redirect.Slug == item.Slug,
                cancellationToken))
        {
            throw new InvalidOperationException("Another content redirect already uses that slug.");
        }

        await ValidateAssetDependenciesAsync(context, page.Id, item, cancellationToken);

        if (!string.Equals(page.Slug, item.Slug, StringComparison.Ordinal))
        {
            var oldSlug = page.Slug;
            page.Slug = item.Slug;
            var existingRedirect = await context.Redirects.SingleOrDefaultAsync(
                redirect => redirect.RouteNamespace == ContentRouteNamespaces.Articles
                    && redirect.Slug == oldSlug,
                cancellationToken);
            if (existingRedirect is null)
            {
                context.Redirects.Add(new ContentRedirectRecord
                {
                    RouteNamespace = ContentRouteNamespaces.Articles,
                    Slug = oldSlug,
                    ContentKey = item.Id
                });
            }
            else if (!string.Equals(existingRedirect.ContentKey, item.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The previous slug is already owned by another redirect.");
            }
        }

        var revision = CreateRevision(page.Id, page.CurrentRevisionId, item);
        context.Revisions.Add(revision);
        await context.SaveChangesAsync(cancellationToken);
        page.CurrentRevisionId = revision.Id;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        item.RevisionId = revision.Id;
        return item;
    }

    public async Task DeleteAsync(
        string sourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        ContentInputValidator.ValidateKey("Slug", slug);
        await using var context = CreateContext(sourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        var page = await context.Pages.SingleOrDefaultAsync(existing => existing.Slug == slug, cancellationToken);
        if (page is null)
        {
            return;
        }

        context.Pages.Remove(page);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(
        string sourceKey,
        string targetSourceKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ValidatePromotionSources(sourceKey, targetSourceKey);
        ContentInputValidator.ValidateKey("Slug", slug);
        await using var sourceContext = CreateContext(sourceKey);
        await using var targetContext = CreateContext(targetSourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(sourceContext, cancellationToken);
        await ContentStorageSchema.EnsureCurrentAsync(targetContext, cancellationToken);

        await using var sourceTransaction = await sourceContext.Database.BeginTransactionAsync(cancellationToken);
        await using var targetTransaction = await targetContext.Database.BeginTransactionAsync(cancellationToken);

        var sourcePage = await sourceContext.Pages
            .Include(page => page.Revisions)
                .ThenInclude(revision => revision.Tags)
            .Include(page => page.Revisions)
                .ThenInclude(revision => revision.Modes)
            .Include(page => page.Revisions)
                .ThenInclude(revision => revision.AssetReferences)
            .Include(page => page.AssetLinks)
                .ThenInclude(link => link.Asset)
            .Include(page => page.CurrentRevision)
            .SingleOrDefaultAsync(page => page.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException("The source page does not exist.");

        var targetPage = await targetContext.Pages
            .Include(page => page.Revisions)
            .Include(page => page.AssetLinks)
            .SingleOrDefaultAsync(page => page.ContentKey == sourcePage.ContentKey, cancellationToken);
        if (targetPage is not null)
        {
            targetContext.Revisions.RemoveRange(targetPage.Revisions);
            targetContext.PageAssets.RemoveRange(targetPage.AssetLinks);
            targetContext.Pages.Remove(targetPage);
            await targetContext.SaveChangesAsync(cancellationToken);
        }

        var sourceAssets = sourcePage.AssetLinks
            .Select(link => link.Asset)
            .Where(asset => asset is not null)
            .DistinctBy(asset => asset!.AssetKey)
            .Cast<ContentAssetRecord>()
            .ToList();
        var targetAssetsBySourceKey = new Dictionary<string, ContentAssetRecord>(StringComparer.Ordinal);
        foreach (var sourceAsset in sourceAssets)
        {
            var targetAsset = await targetContext.Assets.SingleOrDefaultAsync(
                asset => asset.AssetKey == sourceAsset.AssetKey,
                cancellationToken);
            if (targetAsset is null)
            {
                targetAsset = new ContentAssetRecord
                {
                    AssetKey = sourceAsset.AssetKey,
                    FileName = sourceAsset.FileName,
                    MediaType = sourceAsset.MediaType,
                    Sha256 = sourceAsset.Sha256,
                    Data = sourceAsset.Data.ToArray(),
                    CreatedUtc = sourceAsset.CreatedUtc
                };
                targetContext.Assets.Add(targetAsset);
                await targetContext.SaveChangesAsync(cancellationToken);
            }

            targetAssetsBySourceKey[sourceAsset.AssetKey] = targetAsset;
        }

        var copiedPage = new ContentPageRecord
        {
            ContentKey = sourcePage.ContentKey,
            Slug = sourcePage.Slug
        };
        targetContext.Pages.Add(copiedPage);
        await targetContext.SaveChangesAsync(cancellationToken);

        var revisionMap = new Dictionary<long, ContentRevisionRecord>();
        foreach (var sourceRevision in sourcePage.Revisions.OrderBy(revision => revision.Id))
        {
            var copiedRevision = new ContentRevisionRecord
            {
                PageId = copiedPage.Id,
                ParentRevisionId = sourceRevision.ParentRevisionId.HasValue
                    ? revisionMap[sourceRevision.ParentRevisionId.Value].Id
                    : null,
                CreatedUtc = sourceRevision.CreatedUtc,
                BodyFormat = sourceRevision.BodyFormat,
                MetadataJson = sourceRevision.MetadataJson,
                Body = sourceRevision.Body
            };
            copiedRevision.Tags.AddRange(sourceRevision.Tags.Select(tag => new ContentRevisionTagRecord { Tag = tag.Tag }));
            copiedRevision.Modes.AddRange(sourceRevision.Modes.Select(mode => new ContentRevisionModeRecord { SiteMode = mode.SiteMode }));
            copiedRevision.AssetReferences.AddRange(sourceRevision.AssetReferences.Select(asset => new ContentRevisionAssetRecord
            {
                AssetKey = asset.AssetKey,
                Relationship = asset.Relationship
            }));
            targetContext.Revisions.Add(copiedRevision);
            await targetContext.SaveChangesAsync(cancellationToken);
            revisionMap[sourceRevision.Id] = copiedRevision;
        }

        copiedPage.CurrentRevisionId = sourcePage.CurrentRevisionId.HasValue
            ? revisionMap[sourcePage.CurrentRevisionId.Value].Id
            : null;

        foreach (var sourceLink in sourcePage.AssetLinks)
        {
            if (!targetAssetsBySourceKey.TryGetValue(sourceLink.Asset!.AssetKey, out var targetAsset))
            {
                continue;
            }

            targetContext.PageAssets.Add(new ContentPageAssetRecord
            {
                PageId = copiedPage.Id,
                AssetId = targetAsset.Id,
                Relationship = sourceLink.Relationship,
                SortOrder = sourceLink.SortOrder
            });
        }

        var sourceDependencies = await sourceContext.PageAssetDependencies
            .Where(dependency => dependency.PageId == sourcePage.Id)
            .ToListAsync(cancellationToken);
        foreach (var dependency in sourceDependencies)
        {
            targetContext.PageAssetDependencies.Add(new ContentPageAssetDependencyRecord
            {
                PageId = copiedPage.Id,
                AssetKey = dependency.AssetKey
            });
        }

        await targetContext.SaveChangesAsync(cancellationToken);
        await targetTransaction.CommitAsync(cancellationToken);

        sourceContext.Pages.Remove(sourcePage);
        await sourceContext.SaveChangesAsync(cancellationToken);
        await sourceTransaction.CommitAsync(cancellationToken);
    }

    public async Task MoveAllAsync(
        string sourceKey,
        string targetSourceKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePromotionSources(sourceKey, targetSourceKey);
        await using var sourceContext = CreateContext(sourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(sourceContext, cancellationToken);
        var slugs = await sourceContext.Pages
            .AsNoTracking()
            .Select(page => page.Slug)
            .OrderBy(slug => slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await MoveAsync(sourceKey, targetSourceKey, slug, cancellationToken);
        }
    }

    public async Task RestoreRevisionAsync(
        string sourceKey,
        string slug,
        long revisionId,
        long expectedRevisionId,
        CancellationToken cancellationToken = default)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        ContentInputValidator.ValidateKey("Slug", slug);
        await using var context = CreateContext(sourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);

        var page = await context.Pages
            .Include(existing => existing.CurrentRevision)
            .SingleOrDefaultAsync(existing => existing.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException("The content page no longer exists.");
        if (page.CurrentRevisionId != expectedRevisionId)
        {
            throw new ContentAuthoringConflictException(
                "This page changed after the editor was opened. Reload it before restoring a revision.");
        }

        var revision = await context.Revisions
            .Include(existing => existing.Tags)
            .Include(existing => existing.Modes)
            .Include(existing => existing.AssetReferences)
            .SingleOrDefaultAsync(
                existing => existing.PageId == page.Id && existing.Id == revisionId,
                cancellationToken)
            ?? throw new InvalidOperationException("That revision does not exist for this page.");

        var restoredRevision = new ContentRevisionRecord
        {
            PageId = page.Id,
            ParentRevisionId = page.CurrentRevisionId,
            CreatedUtc = DateTime.UtcNow,
            BodyFormat = revision.BodyFormat,
            MetadataJson = revision.MetadataJson,
            Body = revision.Body
        };
        restoredRevision.Tags.AddRange(revision.Tags.Select(tag => new ContentRevisionTagRecord { Tag = tag.Tag }));
        restoredRevision.Modes.AddRange(revision.Modes.Select(mode => new ContentRevisionModeRecord { SiteMode = mode.SiteMode }));
        restoredRevision.AssetReferences.AddRange(revision.AssetReferences.Select(asset => new ContentRevisionAssetRecord
        {
            AssetKey = asset.AssetKey,
            Relationship = asset.Relationship
        }));
        context.Revisions.Add(restoredRevision);
        await context.SaveChangesAsync(cancellationToken);

        page.CurrentRevisionId = restoredRevision.Id;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRevisionAsync(
        string sourceKey,
        string slug,
        long revisionId,
        long expectedRevisionId,
        CancellationToken cancellationToken = default)
    {
        sourceKey = ResolveSourceKey(sourceKey);
        ContentInputValidator.ValidateKey("Slug", slug);
        await using var context = CreateContext(sourceKey);
        await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);

        var page = await context.Pages
            .SingleOrDefaultAsync(existing => existing.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException("The content page no longer exists.");
        if (page.CurrentRevisionId != expectedRevisionId)
        {
            throw new ContentAuthoringConflictException(
                "This page changed after the editor was opened. Reload it before deleting a revision.");
        }
        if (page.CurrentRevisionId == revisionId)
        {
            throw new InvalidOperationException("The current revision can not be deleted.");
        }

        var revision = await context.Revisions.SingleOrDefaultAsync(
            existing => existing.PageId == page.Id && existing.Id == revisionId,
            cancellationToken)
            ?? throw new InvalidOperationException("That revision does not exist for this page.");
        var hasChild = await context.Revisions.AnyAsync(
            existing => existing.ParentRevisionId == revisionId,
            cancellationToken);
        if (hasChild)
        {
            throw new InvalidOperationException("A revision with descendants can not be deleted.");
        }

        context.Revisions.Remove(revision);
        await context.SaveChangesAsync(cancellationToken);
    }

    private void ValidatePromotionSources(string sourceKey, string targetSourceKey)
    {
        if (!string.Equals(sourceKey, _sourceRegistry.AuthoringSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Content promotion must begin in the configured authoring workspace.");
        }
        if (!_sourceRegistry.IsGlobalSource(targetSourceKey))
        {
            throw new InvalidOperationException("Content may only be promoted to a configured Global source.");
        }
        if (string.Equals(sourceKey, targetSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Choose a different target source.");
        }
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
        revision.Modes.AddRange(item.VisibleInModes.Select(modeId => new ContentRevisionModeRecord { SiteMode = modeId }));
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
        if (referencedKeys.Count == 0)
        {
            return;
        }

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
            VisibleModesText = string.Join(Environment.NewLine, item.VisibleInModes.Order(StringComparer.Ordinal)),
            VisibleModesSelection = item.VisibleInModes
                .Order(StringComparer.Ordinal)
                .ToList(),
            BodyFormat = item.BodyFormat,
            Body = item.Body
        };
    }

    private ContentItem ParseAndValidate(ContentAuthoringDocument document, bool requireExistingRevision)
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
            ? document.VisibleModesSelection
            : (document.VisibleModesText ?? string.Empty)
                .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        var normalizedModeIds = selectedModes
            .Select(NormalizeSubmittedModeId)
            .ToList();
        item.VisibleInModes = ContentInputValidator.ParseModes(
            string.Join(Environment.NewLine, normalizedModeIds));

        var unknownModeId = item.VisibleInModes.FirstOrDefault(modeId =>
            !_siteModeRegistry.TryGetById(modeId, out _));
        if (unknownModeId is not null)
        {
            throw new InvalidOperationException(
                $"Unknown or non-hosted site mode '{unknownModeId}'. Content visibility may target only registered site modes.");
        }

        item.BodyFormat = document.BodyFormat.Trim().ToLowerInvariant();
        item.Body = document.Body;

        ContentInputValidator.ValidateItem(item);

        if (!item.Tags.Any(ContentTags.IsContext))
        {
            throw new InvalidOperationException(
                $"At least one context tag is required: {string.Join(", ", ContentTags.ContextTags.Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        if (item.VisibleInModes.Count == 0)
        {
            throw new InvalidOperationException("At least one visible site mode is required.");
        }

        return item;
    }

    private string NormalizeSubmittedModeId(string value)
    {
        if (_siteModeRegistry.TryGetById(value, out var registeredMode))
        {
            return registeredMode!.Id;
        }

        // Compatibility bridge for an editor form opened before stable ids replaced enum
        // names. Newly rendered forms submit stable registered ids directly.
        if (Enum.TryParse<SiteMode>(value, ignoreCase: true, out var legacyMode)
            && Enum.IsDefined(legacyMode)
            && _siteModeRegistry.TryGetByLegacyMode(legacyMode, out var legacyDefinition))
        {
            return legacyDefinition!.Id;
        }

        return value;
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

    private List<ContentAuthoringModeOption> GetModeOptions() => _siteModeRegistry.All
        .Select(mode => new ContentAuthoringModeOption
        {
            Id = mode.Id,
            DisplayName = mode.DisplayName
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

    private static string PrettyMetadata(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static JsonSerializerOptions CreateMetadataJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
