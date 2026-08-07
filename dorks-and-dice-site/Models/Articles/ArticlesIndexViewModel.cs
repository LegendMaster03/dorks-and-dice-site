using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Models.Articles;

public class ArticlesIndexViewModel
{
    public List<ArticleItemViewModel> Articles { get; set; } = [];
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
