using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentAuthoringService : IContentAuthoringService
{
    private static readonly Regex KeyPattern = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions MetadataJsonOptions = CreateMetadataJsonOptions();

    private readonly ContentDbContext _context;
    private readonly IContentRepository _repository;

    public ContentAuthoringService(ContentDbContext context, IContentRepository repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<ContentAuthoringIndexViewModel> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return new ContentAuthoringIndexViewModel
        {
            Items = items
                .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public async Task<ContentAuthoringEditViewModel?> GetEditAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new ContentAuthoringEditViewModel
        {
            Document = ToDocument(item),
            History = await GetHistoryAsync(item.Id, cancellationToken)
        };
    }

    public ContentAuthoringEditViewModel GetNew()
    {
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
                MetadataJson = PrettyMetadata(ContentRecordMapper.SerializeMetadata(metadata)),
                TagsText = ContentTags.Article,
                VisibleModesText = SiteMode.Professional.ToString(),
                BodyFormat = "markdown",
                Body = "## Overview\n\nWrite the page body here."
            }
        };
    }

    public async Task<ContentItem> CreateAsync(
        ContentAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        var item = ParseAndValidate(document, requireExistingRevision: false);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (await _context.Pages.AnyAsync(
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
        _context.Pages.Add(page);
        await _context.SaveChangesAsync(cancellationToken);

        var revision = CreateRevision(page.Id, parentRevisionId: null, item);
        _context.Revisions.Add(revision);
        await _context.SaveChangesAsync(cancellationToken);

        page.CurrentRevisionId = revision.Id;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        item.RevisionId = revision.Id;
        return item;
    }

    public async Task<ContentItem> SaveRevisionAsync(
        ContentAuthoringDocument document,
        CancellationToken cancellationToken = default)
    {
        var item = ParseAndValidate(document, requireExistingRevision: true);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var page = await _context.Pages
            .SingleOrDefaultAsync(candidate => candidate.ContentKey == item.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Content page '{item.Id}' no longer exists.");

        if (page.CurrentRevisionId != document.ExpectedRevisionId)
        {
            throw new ContentAuthoringConflictException(
                "This page changed after the editor was opened. Reload it before saving another revision.");
        }

        if (!string.Equals(page.Slug, item.Slug, StringComparison.OrdinalIgnoreCase)
            && await _context.Pages.AnyAsync(candidate => candidate.Slug == item.Slug && candidate.Id != page.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Another content page already uses slug '{item.Slug}'.");
        }

        page.Slug = item.Slug;
        var revision = CreateRevision(page.Id, page.CurrentRevisionId, item);
        _context.Revisions.Add(revision);
        await _context.SaveChangesAsync(cancellationToken);

        page.CurrentRevisionId = revision.Id;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        item.RevisionId = revision.Id;
        return item;
    }

    private async Task<List<ContentRevisionSummary>> GetHistoryAsync(
        string contentKey,
        CancellationToken cancellationToken)
    {
        var pageId = await _context.Pages
            .Where(page => page.ContentKey == contentKey)
            .Select(page => (long?)page.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!pageId.HasValue)
        {
            return [];
        }

        return await _context.Revisions
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

    private static ContentAuthoringDocument ToDocument(ContentItem item)
    {
        return new ContentAuthoringDocument
        {
            IsNew = false,
            Id = item.Id,
            Slug = item.Slug,
            ExpectedRevisionId = item.RevisionId,
            MetadataJson = PrettyMetadata(ContentRecordMapper.SerializeMetadata(item)),
            TagsText = string.Join(Environment.NewLine, item.Tags.Order(StringComparer.OrdinalIgnoreCase)),
            VisibleModesText = string.Join(Environment.NewLine, item.VisibleInModes.OrderBy(mode => mode.ToString())),
            BodyFormat = item.BodyFormat,
            Body = item.Body
        };
    }

    private static ContentItem ParseAndValidate(ContentAuthoringDocument document, bool requireExistingRevision)
    {
        if (string.IsNullOrWhiteSpace(document.Id) || !KeyPattern.IsMatch(document.Id))
        {
            throw new InvalidOperationException("Stable ID is required and may contain lowercase letters, numbers, and hyphens.");
        }

        if (string.IsNullOrWhiteSpace(document.Slug) || !KeyPattern.IsMatch(document.Slug))
        {
            throw new InvalidOperationException("Slug is required and may contain lowercase letters, numbers, and hyphens.");
        }

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
        item.Tags = ParseValues(document.TagsText, lowercase: true);
        item.VisibleInModes = ParseModes(document.VisibleModesText);
        item.BodyFormat = document.BodyFormat.Trim().ToLowerInvariant();
        item.Body = document.Body ?? string.Empty;

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new InvalidOperationException("Metadata title is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Summary))
        {
            throw new InvalidOperationException("Metadata summary is required.");
        }

        if (!item.Tags.Any(ContentTags.IsContext))
        {
            throw new InvalidOperationException("At least one context tag is required: project, experience, or article.");
        }

        if (item.VisibleInModes.Count == 0)
        {
            throw new InvalidOperationException("At least one visible site mode is required.");
        }

        if (!string.Equals(item.BodyFormat, "markdown", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only the markdown body format is currently supported.");
        }

        if (string.IsNullOrWhiteSpace(item.Body))
        {
            throw new InvalidOperationException("Content body can not be empty.");
        }

        return item;
    }

    private static List<string> ParseValues(string rawValues, bool lowercase)
    {
        return rawValues
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => lowercase ? value.ToLowerInvariant() : value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SiteMode> ParseModes(string rawModes)
    {
        var values = ParseValues(rawModes, lowercase: false);
        var modes = new List<SiteMode>();
        foreach (var value in values)
        {
            if (!Enum.TryParse<SiteMode>(value, ignoreCase: true, out var mode))
            {
                throw new InvalidOperationException($"Unknown site mode '{value}'.");
            }

            modes.Add(mode);
        }

        return modes.Distinct().ToList();
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
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
