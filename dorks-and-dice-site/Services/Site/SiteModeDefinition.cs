using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed record SiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode? LegacyMode,
    string ViewFolder,
    string AssetFolder,
    bool SupportsContent = true,
    bool SupportsScopedEditor = true,
    bool IsPreviewable = true);

public static class BuiltInSiteModes
{
    public static SiteModeDefinition Unassigned { get; } = new(
        Id: "unassigned",
        DisplayName: "Unassigned",
        LegacyMode: SiteMode.Unassigned,
        ViewFolder: "Unassigned",
        AssetFolder: "unassigned",
        SupportsContent: false,
        SupportsScopedEditor: false,
        IsPreviewable: false);

    // Dorks & Dice and Professional represent the normal mode shape. Future normal
    // modes inherit content, scoped-editor, and preview behavior unless they explicitly
    // opt out for a special-case reason.
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

    public static SiteModeDefinition Development { get; } = new(
        Id: "development",
        DisplayName: "Development",
        LegacyMode: SiteMode.Development,
        ViewFolder: "Development",
        AssetFolder: "development",
        SupportsContent: false,
        SupportsScopedEditor: false);

    public static IReadOnlyList<SiteModeDefinition> All { get; } =
    [
        Unassigned,
        DorksAndDice,
        Professional,
        Development
    ];

    public static bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }
}
