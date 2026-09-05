using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Modes.DorksAndDice;

/// <summary>
/// Intrinsic definition of the Dorks & Dice standard mode. Deployment hostnames and
/// infrastructure configuration intentionally do not belong here.
/// </summary>
public static class DorksAndDiceMode
{
    public static SiteModeDefinition Definition { get; } = new(
        Id: "dorks-and-dice",
        DisplayName: "Dorks & Dice",
        LegacyMode: SiteMode.DorksAndDice,
        ViewFolder: "DorksAndDice",
        AssetFolder: "dorks-and-dice");
}
