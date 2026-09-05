using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Modes.Professional;

/// <summary>
/// Intrinsic definition of the Professional standard mode. Deployment hostnames and
/// infrastructure configuration intentionally do not belong here.
/// </summary>
public static class ProfessionalMode
{
    public static SiteModeDefinition Definition { get; } = new(
        Id: "professional",
        DisplayName: "Professional",
        LegacyMode: SiteMode.Professional,
        ViewFolder: "Professional",
        AssetFolder: "professional")
    {
        OwnedRoutePrefixes = ["/resume"],
        AdditionalAssetPaths = ["/site-modes/dorks-and-dice/images/favicon.svg"],
        SitemapPaths = ["/", "/resume", "/articles"],
        ShowAnonymousLoginInNavigation = false
    };
}
