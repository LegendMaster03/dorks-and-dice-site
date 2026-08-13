namespace dorks_and_dice_site.Models.Content;

public sealed class ContentDetailViewModel
{
    public required ContentItem Item { get; init; }
    public required string ContextTag { get; init; }
    public required string RenderedBodyHtml { get; init; }
    public List<ContentNavigationLink> BackLinks { get; init; } = [];
    public bool IsDevelopmentVisibilityOverride { get; init; }
}
