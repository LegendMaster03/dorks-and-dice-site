using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeBrandingPartUnavailableException : InvalidOperationException
{
    public SiteModeBrandingPartUnavailableException(SiteModeBrandingPart brandingPart)
        : base($"The requested branding part is not available: {brandingPart}.")
    {
        BrandingPart = brandingPart;
    }

    public SiteModeBrandingPart BrandingPart { get; }
}
