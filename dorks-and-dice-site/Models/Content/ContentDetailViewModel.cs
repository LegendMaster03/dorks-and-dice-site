namespace dorks_and_dice_site.Models.Content;

/// <summary>
/// Published content detail projection with sanitized rendered HTML and
/// request-dependent navigation and Development visibility flags.
/// </summary>
public sealed class ContentDetailViewModel
{
    public required ContentItem Item { get; init; }
    public required string ContextTag { get; init; }
    public required string RenderedBodyHtml { get; init; }
    public List<ContentNavigationLink> BackLinks { get; init; } = [];
    public bool IsDevelopmentVisibilityOverride { get; init; }
    public bool IsDevelopmentPreview { get; init; }
    public string? EditHref { get; init; }
}
