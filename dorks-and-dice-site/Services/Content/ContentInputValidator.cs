using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Content;

internal static class ContentInputValidator
{
    private static readonly Regex KeyPattern = new(
        "^[a-z0-9][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex TagPattern = new(
        "^[a-z0-9][a-z0-9:_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex CssClassListPattern = new(
        "^[A-Za-z0-9_-]+(?:[ \\t]+[A-Za-z0-9_-]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static void ValidateDocumentShape(ContentAuthoringDocument document)
    {
        ValidateKey("Stable ID", document.Id);
        ValidateKey("Slug", document.Slug);
        ValidateLength("Metadata JSON", document.MetadataJson, ContentInputPolicy.MaxMetadataJsonLength);
        ValidateLength("Tags", document.TagsText, ContentInputPolicy.MaxTagTextLength);
        ValidateLength("Visible modes", document.VisibleModesText, ContentInputPolicy.MaxModesTextLength);
        ValidateLength("Body", document.Body, ContentInputPolicy.MaxBodyLength);

        if (!string.Equals(document.BodyFormat?.Trim(), "markdown", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only the markdown body format is supported.");
        }
    }

    public static List<string> ParseTags(string rawValues)
    {
        ValidateLength("Tags", rawValues, ContentInputPolicy.MaxTagTextLength);
        var values = SplitValues(rawValues, lowercase: true);
        if (values.Count > ContentInputPolicy.MaxTags)
        {
            throw new InvalidOperationException($"Content may contain at most {ContentInputPolicy.MaxTags} tags.");
        }

        foreach (var tag in values)
        {
            if (tag.Length > ContentInputPolicy.MaxTagLength || !TagPattern.IsMatch(tag))
            {
                throw new InvalidOperationException(
                    $"Tag '{tag}' is invalid. Tags may contain lowercase letters, numbers, colons, underscores, and hyphens.");
            }

            if (ContentTags.IsInternal(tag))
            {
                throw new InvalidOperationException("Internal content tags can not be supplied through the authoring form.");
            }
        }

        return values;
    }

    public static List<SiteMode> ParseModes(string rawModes)
    {
        ValidateLength("Visible modes", rawModes, ContentInputPolicy.MaxModesTextLength);
        var values = SplitValues(rawModes, lowercase: false);
        var modes = new List<SiteMode>();
        foreach (var value in values)
        {
            if (!Enum.TryParse<SiteMode>(value, ignoreCase: true, out var mode)
                || !Enum.IsDefined(mode)
                || int.TryParse(value, out _))
            {
                throw new InvalidOperationException($"Unknown site mode '{value}'.");
            }

            modes.Add(mode);
        }

        return modes.Distinct().ToList();
    }

    public static void ValidateItem(ContentItem item)
    {
        ValidateRequiredText("Title", item.Title, ContentInputPolicy.MaxTitleLength);
        ValidateRequiredText("Summary", item.Summary, ContentInputPolicy.MaxSummaryLength);
        ValidateOptionalText("Subtitle", item.Subtitle, ContentInputPolicy.MaxShortTextLength);
        ValidateOptionalText("Date text", item.DateText, ContentInputPolicy.MaxShortTextLength);
        ValidateOptionalText("Category", item.Category, ContentInputPolicy.MaxShortTextLength);
        ValidateOptionalText("Link text", item.LinkText, ContentInputPolicy.MaxShortTextLength);
        ValidateOptionalText("Meta title", item.MetaTitle, ContentInputPolicy.MaxTitleLength);
        ValidateOptionalText("Meta description", item.MetaDescription, ContentInputPolicy.MaxSummaryLength);

        ValidateLinkUrl("Repository URL", item.RepositoryUrl, allowMailto: false);
        ValidateAssetUrl("Meta image", item.MetaImage);

        if (item.ListingImage is not null)
        {
            ValidateAssetUrl("Listing image URL", item.ListingImage.Url, required: true);
            ValidateRequiredText("Listing image alt text", item.ListingImage.AltText, ContentInputPolicy.MaxShortTextLength);
            if (item.ListingImage.Width < 0 || item.ListingImage.Height < 0)
            {
                throw new InvalidOperationException("Listing image dimensions can not be negative.");
            }
        }

        ValidateTextList("Highlights", item.Highlights, ContentInputPolicy.MaxHighlights, ContentInputPolicy.MaxHighlightLength);

        if (item.Presentations.Count > ContentTags.ContextTags.Count)
        {
            throw new InvalidOperationException("Too many context presentations were supplied.");
        }

        foreach (var (context, presentation) in item.Presentations)
        {
            if (!ContentTags.ContextTags.Contains(context))
            {
                throw new InvalidOperationException($"Unknown presentation context '{context}'.");
            }

            if (presentation is null)
            {
                throw new InvalidOperationException($"Presentation '{context}' can not be null.");
            }

            ValidateOptionalText($"{context} title", presentation.Title, ContentInputPolicy.MaxTitleLength);
            ValidateOptionalText($"{context} subtitle", presentation.Subtitle, ContentInputPolicy.MaxShortTextLength);
            ValidateOptionalText($"{context} summary", presentation.Summary, ContentInputPolicy.MaxSummaryLength);
            ValidateOptionalText($"{context} date text", presentation.DateText, ContentInputPolicy.MaxShortTextLength);
            ValidateOptionalText($"{context} category", presentation.Category, ContentInputPolicy.MaxShortTextLength);
            ValidateOptionalText($"{context} link text", presentation.LinkText, ContentInputPolicy.MaxShortTextLength);
            if (presentation.Highlights is not null)
            {
                ValidateTextList(
                    $"{context} highlights",
                    presentation.Highlights,
                    ContentInputPolicy.MaxHighlights,
                    ContentInputPolicy.MaxHighlightLength);
            }
        }

        if (item.Header is null)
        {
            throw new InvalidOperationException("Content header metadata can not be null.");
        }

        ValidateOptionalText("Header meta line", item.Header.MetaLine, ContentInputPolicy.MaxShortTextLength);
        ValidateAssetUrl("Header logo URL", item.Header.LogoUrl);
        ValidateOptionalText("Header logo alt text", item.Header.LogoAltText, ContentInputPolicy.MaxShortTextLength);
        ValidateLinkUrl("Header logo link URL", item.Header.LogoLinkUrl, allowMailto: true);
        ValidateOptionalText("Header logo aria label", item.Header.LogoAriaLabel, ContentInputPolicy.MaxShortTextLength);

        if (!string.IsNullOrWhiteSpace(item.Header.CssClass))
        {
            ValidateLength("Header CSS class", item.Header.CssClass, 256);
            if (!CssClassListPattern.IsMatch(item.Header.CssClass))
            {
                throw new InvalidOperationException("Header CSS classes contain unsupported characters.");
            }
        }

        if (item.Header.InfoItems.Count > ContentInputPolicy.MaxInfoItems
            || item.Header.InfoItemLinks.Count > ContentInputPolicy.MaxInfoItems)
        {
            throw new InvalidOperationException($"Header metadata may contain at most {ContentInputPolicy.MaxInfoItems} info items.");
        }

        foreach (var (key, value) in item.Header.InfoItems)
        {
            ValidateRequiredText("Header info key", key, ContentInputPolicy.MaxShortTextLength);
            ValidateRequiredText($"Header info value '{key}'", value, ContentInputPolicy.MaxShortTextLength);
        }

        foreach (var (key, value) in item.Header.InfoItemLinks)
        {
            ValidateRequiredText("Header link key", key, ContentInputPolicy.MaxShortTextLength);
            ValidateLinkUrl($"Header info link '{key}'", value, allowMailto: true, required: true);
        }
    }

    public static void ValidateKey(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > ContentInputPolicy.MaxKeyLength
            || !KeyPattern.IsMatch(value))
        {
            throw new InvalidOperationException(
                $"{fieldName} is required, may be at most {ContentInputPolicy.MaxKeyLength} characters, and may contain only lowercase letters, numbers, and hyphens.");
        }
    }

    private static List<string> SplitValues(string? rawValues, bool lowercase)
    {
        if (string.IsNullOrWhiteSpace(rawValues))
        {
            return [];
        }

        return rawValues
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => lowercase ? value.ToLowerInvariant() : value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateTextList(string fieldName, IReadOnlyCollection<string> values, int maxCount, int maxLength)
    {
        if (values.Count > maxCount)
        {
            throw new InvalidOperationException($"{fieldName} may contain at most {maxCount} entries.");
        }

        foreach (var value in values)
        {
            ValidateRequiredText(fieldName, value, maxLength);
        }
    }

    private static void ValidateRequiredText(string fieldName, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        ValidateLength(fieldName, value, maxLength);
    }

    private static void ValidateOptionalText(string fieldName, string? value, int maxLength)
    {
        if (value is not null)
        {
            ValidateLength(fieldName, value, maxLength);
        }
    }

    private static void ValidateLength(string fieldName, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} exceeds the maximum length of {maxLength:N0} characters.");
        }
    }

    private static void ValidateAssetUrl(string fieldName, string? value, bool required = false)
    {
        ValidateUrl(fieldName, value, allowMailto: false, required);
    }

    private static void ValidateLinkUrl(string fieldName, string? value, bool allowMailto, bool required = false)
    {
        ValidateUrl(fieldName, value, allowMailto, required);
    }

    private static void ValidateUrl(string fieldName, string? value, bool allowMailto, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new InvalidOperationException($"{fieldName} is required.");
            }

            return;
        }

        ValidateLength(fieldName, value, ContentInputPolicy.MaxUrlLength);
        if (value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{fieldName} contains control characters.");
        }

        if ((value.StartsWith('/', StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal))
            || value.StartsWith('#', StringComparison.Ordinal))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{fieldName} must be an absolute HTTP(S) URL or a root-relative URL.");
        }

        var schemeAllowed = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (allowMailto && string.Equals(uri.Scheme, Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase));
        if (!schemeAllowed)
        {
            throw new InvalidOperationException($"{fieldName} uses an unsupported URL scheme.");
        }
    }
}
