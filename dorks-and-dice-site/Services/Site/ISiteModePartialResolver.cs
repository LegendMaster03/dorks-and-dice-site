using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModePartialResolver
{
    string GetPartialPath(SiteMode siteMode, string partialName);
    string GetBrandingPartialPath(SiteMode siteMode, SiteModeBrandingPart brandingPart);
}
