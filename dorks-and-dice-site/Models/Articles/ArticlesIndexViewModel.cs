using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Models.Articles;

public class ArticlesIndexViewModel
{
    public List<ArticleItemViewModel> Articles { get; set; } = [];
    public bool ProfessionalFilterActive { get; set; }
    public SiteMode SiteMode { get; set; } = SiteMode.DorksAndDice;
    public bool IsDevelopmentPreview { get; set; }
    public bool IncludeUnlistedActive { get; set; }
    public bool ShowSearchFilter { get; set; }
    public bool ShowSearchFilterOnProfessional { get; set; }
    public bool ShowCategoryFilter { get; set; }
    public bool ShowCategoryFilterOnProfessional { get; set; }
    public bool ShowProfessionalFilter { get; set; }
    public bool ShowProfessionalFilterOnProfessional { get; set; }
    public List<string> Categories { get; set; } = [];

    public bool IsProfessionalMode => SiteMode == SiteMode.Professional;

    public bool ShouldShowSearchFilter => ShowSearchFilter && (!IsProfessionalMode || ShowSearchFilterOnProfessional);
    public bool ShouldShowCategoryFilter => ShowCategoryFilter && (!IsProfessionalMode || ShowCategoryFilterOnProfessional);
    public bool ShouldShowProfessionalFilter => ShowProfessionalFilter && (!IsProfessionalMode || ShowProfessionalFilterOnProfessional);
}
