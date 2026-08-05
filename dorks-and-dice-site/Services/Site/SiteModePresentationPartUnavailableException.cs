using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModePresentationPartUnavailableException : InvalidOperationException
{
    public SiteModePresentationPartUnavailableException(SiteModePresentationPart presentationPart)
        : base($"The requested presentation part is not available: {presentationPart}.")
    {
        PresentationPart = presentationPart;
    }

    public SiteModePresentationPart PresentationPart { get; }
}
