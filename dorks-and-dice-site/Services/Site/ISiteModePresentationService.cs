using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModePresentationService
{
    string GetTitleSuffix(SiteMode siteMode);
    string GetDefaultMetaDescription(SiteMode siteMode);
    string GetFaviconPath(SiteMode siteMode);
    ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteMode siteMode);
}
