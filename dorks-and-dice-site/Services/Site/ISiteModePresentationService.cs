using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModePresentationService
{
    string GetTitleSuffix(SiteModeContext context);
    string GetDefaultMetaDescription(SiteModeContext context);
    string GetFaviconPath(SiteModeContext context);
    string? GetDefaultMetaImagePath(SiteModeContext context);
    string? GetStructuredDataJson(SiteModeContext context, string canonicalOrigin);
    ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteModeContext context);

    // Compatibility overloads for callers that have not yet migrated off the legacy enum.
    string GetTitleSuffix(SiteMode siteMode);
    string GetDefaultMetaDescription(SiteMode siteMode);
    string GetFaviconPath(SiteMode siteMode);
    string? GetDefaultMetaImagePath(SiteMode siteMode);
    string? GetStructuredDataJson(SiteMode siteMode, string canonicalOrigin);
    ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteMode siteMode);
}
