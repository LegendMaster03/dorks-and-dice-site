using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModePartialResolver
{
    string GetPartialPath(SiteModeContext context, string partialName);
    string GetBrandingPartialPath(SiteModeContext context, SiteModeBrandingPart brandingPart);

    // Compatibility overloads for callers that have not yet migrated off the legacy enum.
    string GetPartialPath(SiteMode siteMode, string partialName);
    string GetBrandingPartialPath(SiteMode siteMode, SiteModeBrandingPart brandingPart);
}
