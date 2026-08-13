using System.Text.Json.Serialization;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Models.Content;

public static class ContentTags
{
    public const string Project = "project";
    public const string Experience = "experience";
    public const string Article = "article";
    public const string Unlisted = "_internal:unlisted";

    public static readonly IReadOnlySet<string> ContextTags = new HashSet<string>(
        [Project, Experience, Article],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsContext(string tag) => ContextTags.Contains(tag);

    public static bool IsInternal(string tag) => tag.StartsWith("_internal:", StringComparison.OrdinalIgnoreCase);

    public static bool IsPublic(string tag) => !IsContext(tag) && !IsInternal(tag);
}

public sealed class ContentItem
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
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
    public ContentDetailHeader Header { get; set; } = new();
    public List<string> Highlights { get; set; } = [];
    public Dictionary<string, ContentPresentation> Presentations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SiteMode> VisibleInModes { get; set; } = [];
    public List<string> Tags { get; set; } = [];

    [JsonIgnore]
    public string BodyMarkdown { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsListed => !Tags.Any(tag => string.Equals(tag, ContentTags.Unlisted, StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public IReadOnlyList<string> PublicTags => Tags.Where(ContentTags.IsPublic).ToList();

    public bool HasTag(string tag) => Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase));

    public bool IsVisibleInMode(SiteMode siteMode) => siteMode == SiteMode.Development || VisibleInModes.Contains(siteMode);

    public ContentPresentation? GetPresentation(string contextTag)
    {
        if (Presentations.TryGetValue(contextTag, out var presentation))
        {
            return presentation;
        }

        return Presentations
            .FirstOrDefault(pair => string.Equals(pair.Key, contextTag, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    public string GetTitle(string contextTag) => GetPresentation(contextTag)?.Title ?? Title;
    public string? GetSubtitle(string contextTag) => GetPresentation(contextTag)?.Subtitle ?? Subtitle;
    public string GetSummary(string contextTag) => GetPresentation(contextTag)?.Summary ?? Summary;
    public string? GetDateText(string contextTag) => GetPresentation(contextTag)?.DateText ?? DateText;
    public string? GetCategory(string contextTag) => GetPresentation(contextTag)?.Category ?? Category;
    public string GetLinkText(string contextTag) => GetPresentation(contextTag)?.LinkText ?? LinkText;
    public bool IsFeatured(string contextTag) => GetPresentation(contextTag)?.Featured ?? Featured;
    public IReadOnlyList<string> GetHighlights(string contextTag) => GetPresentation(contextTag)?.Highlights ?? Highlights;
}

public sealed class ContentPresentation
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Summary { get; set; }
    public string? DateText { get; set; }
    public string? Category { get; set; }
    public string? LinkText { get; set; }
    public bool? Featured { get; set; }
    public List<string>? Highlights { get; set; }
}

public sealed class ContentDetailHeader
{
    public string? MetaLine { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoAltText { get; set; }
    public string? LogoLinkUrl { get; set; }
    public string? LogoAriaLabel { get; set; }
    public Dictionary<string, string> InfoItems { get; set; } = [];
    public Dictionary<string, string> InfoItemLinks { get; set; } = [];
}
