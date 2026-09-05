using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModeStylesheetResolver
{
    IReadOnlyList<string> GetStylesheetPaths(SiteModeContext context);

    // Compatibility overload for callers that have not yet migrated off the legacy enum.
    IReadOnlyList<string> GetStylesheetPaths(SiteMode siteMode, bool includeDevelopmentTools);
}
