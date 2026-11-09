using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Indicates that a presentation module cannot supply a requested part,
/// allowing the shared resolver to distinguish absence from other failures.
/// </summary>
public sealed class SiteModePresentationPartUnavailableException : InvalidOperationException
{
    public SiteModePresentationPartUnavailableException(SiteModePresentationPart presentationPart)
        : base($"The requested presentation part is not available: {presentationPart}.")
    {
        PresentationPart = presentationPart;
    }

    public SiteModePresentationPart PresentationPart { get; }
}
