using System.Text.Json;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content.Storage;

internal static class ContentRecordMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ContentItem ToContentItem(ContentPageRecord page, ContentRevisionRecord revision)
    {
        var metadata = JsonSerializer.Deserialize<ContentRevisionMetadata>(revision.MetadataJson, JsonOptions)
            ?? throw new InvalidOperationException($"Revision {revision.Id} contains invalid content metadata.");

        var modes = revision.Modes
            .Select(modeRecord => NormalizeStoredModeId(revision.Id, modeRecord.SiteMode))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ContentItem
        {
            Id = page.ContentKey,
            Slug = page.Slug,
            RevisionId = revision.Id,
            Title = metadata.Title,
            Subtitle = metadata.Subtitle,
            Summary = metadata.Summary,
            DateText = metadata.DateText,
            Category = metadata.Category,
            RepositoryUrl = metadata.RepositoryUrl,
            LinkText = metadata.LinkText,
            Featured = metadata.Featured,
            MetaTitle = metadata.MetaTitle,
            MetaDescription = metadata.MetaDescription,
            MetaImage = metadata.MetaImage,
            ListingImage = metadata.ListingImage,
            Header = metadata.Header ?? new ContentDetailHeader(),
            Highlights = metadata.Highlights ?? [],
            Presentations = metadata.Presentations ?? new Dictionary<string, ContentPresentation>(StringComparer.OrdinalIgnoreCase),
            VisibleInModes = modes,
            Tags = revision.Tags.Select(tag => tag.Tag).ToList(),
            BodyFormat = revision.BodyFormat,
            Body = revision.Body
        };
    }

    public static string SerializeMetadata(ContentItem item)
    {
        var metadata = new ContentRevisionMetadata
        {
            Title = item.Title,
            Subtitle = item.Subtitle,
            Summary = item.Summary,
            DateText = item.DateText,
            Category = item.Category,
            RepositoryUrl = item.RepositoryUrl,
            LinkText = item.LinkText,
            Featured = item.Featured,
            MetaTitle = item.MetaTitle,
            MetaDescription = item.MetaDescription,
            MetaImage = item.MetaImage,
            ListingImage = item.ListingImage,
            Header = item.Header,
            Highlights = item.Highlights,
            Presentations = item.Presentations
        };

        return JsonSerializer.Serialize(metadata, JsonOptions);
    }

    private static string NormalizeStoredModeId(long revisionId, string storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            throw new InvalidOperationException($"Revision {revisionId} contains an empty site mode.");
        }

        var value = storedValue.Trim();
        var registeredBuiltIn = BuiltInSiteModes.All.FirstOrDefault(mode =>
            string.Equals(mode.Id, value, StringComparison.OrdinalIgnoreCase));
        if (registeredBuiltIn is not null)
        {
            return registeredBuiltIn.Id;
        }

        // Revisions created before stable ids were introduced stored SiteMode enum names.
        // Convert those built-in legacy values at the storage boundary while preserving any
        // already-stable deployment-defined id without requiring an enum member.
        if (Enum.TryParse<SiteMode>(value, ignoreCase: true, out var legacyMode)
            && Enum.IsDefined(legacyMode))
        {
            if (BuiltInSiteModes.TryGetByLegacyMode(legacyMode, out var definition))
            {
                return definition!.Id;
            }

            throw new InvalidOperationException(
                $"Revision {revisionId} contains framework runtime state '{value}' as a content visibility mode.");
        }

        return value;
    }

    private sealed class ContentRevisionMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? DateText { get; set; }
        public string? Category { get; set; }
        public string? RepositoryUrl { get; set; }
        public string LinkText { get; set; } = "Open details";
        public bool Featured { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaImage { get; set; }
        public ContentImage? ListingImage { get; set; }
        public ContentDetailHeader? Header { get; set; }
        public List<string>? Highlights { get; set; }
        public Dictionary<string, ContentPresentation>? Presentations { get; set; }
    }
}
