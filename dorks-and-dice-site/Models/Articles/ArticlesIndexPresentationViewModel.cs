namespace dorks_and_dice_site.Models.Articles;

public sealed class ArticlesIndexPresentationViewModel
{
    public string MetaTitle { get; init; } = string.Empty;
    public string MetaDescription { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = "Articles";
    public string Title { get; init; } = "Long-Form Write-Ups";
    public string Description { get; init; } = string.Empty;
    public string EmptyStateText { get; init; } = "No listed articles are available for this site mode yet.";
    public bool ShowSearchFilter { get; init; } = true;
    public bool ShowCategoryFilter { get; init; } = true;
}
