using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Articles;

public interface IArticleCatalogService
{
    ArticlesIndexViewModel GetIndex(SiteMode siteMode, bool includeUnlisted, bool isDevelopmentPreview);
    ArticleItemViewModel? GetByAction(string action);
}
