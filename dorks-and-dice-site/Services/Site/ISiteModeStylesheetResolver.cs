using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Selects normal-mode and synthetic Development stylesheets independently.
/// The enum overload is retained for callers awaiting the registry migration.
/// </summary>
public interface ISiteModeStylesheetResolver
{
    IReadOnlyList<string> GetStylesheetPaths(SiteModeContext context);

    // Compatibility overload for callers that have not yet migrated off the legacy enum.
    IReadOnlyList<string> GetStylesheetPaths(SiteMode siteMode, bool includeDevelopmentTools);
}
