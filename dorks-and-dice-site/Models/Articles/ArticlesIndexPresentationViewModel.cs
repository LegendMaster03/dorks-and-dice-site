namespace dorks_and_dice_site.Models.Articles;

/// <summary>
/// Mode-owned labels and filter visibility for the shared articles list.
/// Content queries stay in the catalog rather than this presentation model.
/// </summary>
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
