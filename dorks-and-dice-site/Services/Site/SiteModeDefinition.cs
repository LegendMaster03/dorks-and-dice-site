using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Describes a normal hosted site mode. Framework fallback behavior and Trusted Preview
/// are intentionally not represented by this type.
/// </summary>
public sealed record SiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode? LegacyMode,
    string ViewFolder,
    string AssetFolder)
{
    // Normal site modes participate in the shared content and scoped-capability model.
    // These properties remain during migration so existing consumers can move to the
    // registry without introducing per-mode opt-in flags.
    public bool SupportsContent => true;
    public bool SupportsScopedEditor => true;
}

public static class BuiltInSiteModes
{
    public static SiteModeDefinition DorksAndDice { get; } = new(
        Id: "dorks-and-dice",
        DisplayName: "Dorks & Dice",
        LegacyMode: SiteMode.DorksAndDice,
        ViewFolder: "DorksAndDice",
        AssetFolder: "dorks-and-dice");

    public static SiteModeDefinition Professional { get; } = new(
        Id: "professional",
        DisplayName: "Professional",
        LegacyMode: SiteMode.Professional,
        ViewFolder: "Professional",
        AssetFolder: "professional");

    /// <summary>
    /// The normal site modes shipped by this deployment. Framework fallback and Trusted
    /// Preview are deliberately absent because they are not site modes.
    /// </summary>
    public static IReadOnlyList<SiteModeDefinition> All { get; } =
    [
        DorksAndDice,
        Professional
    ];

    public static bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }
}
