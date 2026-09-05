namespace dorks_and_dice_site.Models.Content;

public sealed class HomepageContentViewModel
{
    public required ContentItem Item { get; init; }
    public required string RenderedBodyHtml { get; init; }
}
