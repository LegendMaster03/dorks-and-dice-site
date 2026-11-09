namespace dorks_and_dice_site.Models.Content;

/// <summary>
/// Navigation back-link emitted by public content detail routes.
/// </summary>
public sealed class ContentNavigationLink
{
    public required string Text { get; init; }
    public required string Href { get; init; }
}
