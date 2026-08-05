using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModeStylesheetResolver
{
    IReadOnlyList<string> GetStylesheetPaths(SiteMode siteMode, bool includeDevelopmentTools);
}
