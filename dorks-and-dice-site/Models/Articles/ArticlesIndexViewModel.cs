using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Models.Articles;

/// <summary>
/// Combines the effective articles catalog with mode-specific presentation
/// and Development preview visibility. SiteMode is a legacy view projection.
/// </summary>
public class ArticlesIndexViewModel
{
    public List<ContentItem> Articles { get; set; } = [];
    public ArticlesIndexPresentationViewModel Presentation { get; set; } = new();
    public SiteMode SiteMode { get; set; } = SiteMode.DorksAndDice;
    public bool IsDevelopmentPreview { get; set; }
    public bool IncludeUnlistedActive { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];

    public bool ShouldShowSearchFilter => Presentation.ShowSearchFilter;
    public bool ShouldShowCategoryFilter => Presentation.ShowCategoryFilter;
    public bool ShouldShowTagFilter => Tags.Count > 0;
}
