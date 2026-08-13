using System.Text.Json;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Content.Storage;

internal static class ContentRecordMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ContentItem ToContentItem(ContentPageRecord page, ContentRevisionRecord revision)
    {
        var metadata = JsonSerializer.Deserialize<ContentRevisionMetadata>(revision.MetadataJson, JsonOptions)
            ?? throw new InvalidOperationException($"Revision {revision.Id} contains invalid content metadata.");

        var modes = new List<SiteMode>();
        foreach (var modeRecord in revision.Modes)
        {
            if (!Enum.TryParse<SiteMode>(modeRecord.SiteMode, true, out var mode))
            {
                throw new InvalidOperationException(
                    $"Revision {revision.Id} contains invalid site mode '{modeRecord.SiteMode}'.");
            }

            modes.Add(mode);
        }

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
            Header = item.Header,
            Highlights = item.Highlights,
            Presentations = item.Presentations
        };

        return JsonSerializer.Serialize(metadata, JsonOptions);
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
        public ContentDetailHeader? Header { get; set; }
        public List<string>? Highlights { get; set; }
        public Dictionary<string, ContentPresentation>? Presentations { get; set; }
    }
}
