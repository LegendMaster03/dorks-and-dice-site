using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModePresentationModule
{
    SiteMode SiteMode { get; }
    string GetTitleSuffix();
    string GetDefaultMetaDescription();
    string GetFaviconPath();
    ArticlesIndexPresentationViewModel GetArticlesIndexPresentation();
}
