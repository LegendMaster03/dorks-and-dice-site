using dorks_and_dice_site.Models.Articles;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Provides non-view presentation for a registered presentation key. Normal site modes use
/// their stable mode id. Framework fallback and Trusted Preview retain compatibility keys
/// during migration and will move to their dedicated framework areas when files are
/// physically reorganized.
/// </summary>
public interface ISiteModePresentationModule
{
    string PresentationKey { get; }
    string GetTitleSuffix();
    string GetDefaultMetaDescription();
    string GetFaviconPath();
    ArticlesIndexPresentationViewModel GetArticlesIndexPresentation();
}
