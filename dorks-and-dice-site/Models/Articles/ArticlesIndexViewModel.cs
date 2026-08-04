namespace dorks_and_dice_site.Models.Articles;

public class ArticlesIndexViewModel
{
    public List<ArticleItemViewModel> Articles { get; set; } = [];
    public bool ProfessionalFilterActive { get; set; }
    public bool IsProfessionalDomain { get; set; }
    public bool ShowSearchFilter { get; set; }
    public bool ShowSearchFilterOnProfessional { get; set; }
    public bool ShowCategoryFilter { get; set; }
    public bool ShowCategoryFilterOnProfessional { get; set; }
    public bool ShowProfessionalFilter { get; set; }
    public bool ShowProfessionalFilterOnProfessional { get; set; }
    public List<string> Categories { get; set; } = [];

    public bool ShouldShowSearchFilter => ShowSearchFilter && (!IsProfessionalDomain || ShowSearchFilterOnProfessional);
    public bool ShouldShowCategoryFilter => ShowCategoryFilter && (!IsProfessionalDomain || ShowCategoryFilterOnProfessional);
    public bool ShouldShowProfessionalFilter => ShowProfessionalFilter && (!IsProfessionalDomain || ShowProfessionalFilterOnProfessional);
}
