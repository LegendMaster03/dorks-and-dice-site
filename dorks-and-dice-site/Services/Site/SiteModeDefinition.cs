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

    /// <summary>
    /// Route prefixes owned by this mode in addition to the framework's shared mode-adaptive
    /// routes. Prefixes match both the exact path and descendants below that path.
    /// </summary>
    public IReadOnlyList<string> OwnedRoutePrefixes { get; init; } = [];

    /// <summary>
    /// Exact static-asset paths this mode may use outside its own asset folder. This is for
    /// narrow compatibility exceptions, not general cross-mode asset sharing.
    /// </summary>
    public IReadOnlyList<string> AdditionalAssetPaths { get; init; } = [];
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
        AssetFolder: "professional")
    {
        OwnedRoutePrefixes = ["/resume"],
        AdditionalAssetPaths = ["/site-modes/dorks-and-dice/images/favicon.svg"]
    };

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
