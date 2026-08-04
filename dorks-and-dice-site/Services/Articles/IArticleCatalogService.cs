using dorks_and_dice_site.Models.Articles;

namespace dorks_and_dice_site.Services.Articles;

public interface IArticleCatalogService
{
    ArticlesIndexViewModel GetIndex(bool professionalOnly);
    ArticleItemViewModel? GetByAction(string action);
}
