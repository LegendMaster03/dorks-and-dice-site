using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed record SiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode? LegacyMode,
    bool SupportsContent,
    bool SupportsScopedEditor,
    bool IsPreviewable,
    string ViewFolder,
    string AssetFolder);

public static class BuiltInSiteModes
{
    public static SiteModeDefinition Unassigned { get; } = new(
        Id: "unassigned",
        DisplayName: "Unassigned",
        LegacyMode: SiteMode.Unassigned,
        SupportsContent: false,
        SupportsScopedEditor: false,
        IsPreviewable: false,
        ViewFolder: "Unassigned",
        AssetFolder: "unassigned");

    public static SiteModeDefinition DorksAndDice { get; } = new(
        Id: "dorks-and-dice",
        DisplayName: "Dorks & Dice",
        LegacyMode: SiteMode.DorksAndDice,
        SupportsContent: true,
        SupportsScopedEditor: true,
        IsPreviewable: true,
        ViewFolder: "DorksAndDice",
        AssetFolder: "dorks-and-dice");

    public static SiteModeDefinition Professional { get; } = new(
        Id: "professional",
        DisplayName: "Professional",
        LegacyMode: SiteMode.Professional,
        SupportsContent: true,
        SupportsScopedEditor: true,
        IsPreviewable: true,
        ViewFolder: "Professional",
        AssetFolder: "professional");

    public static SiteModeDefinition Development { get; } = new(
        Id: "development",
        DisplayName: "Development",
        LegacyMode: SiteMode.Development,
        SupportsContent: false,
        SupportsScopedEditor: false,
        IsPreviewable: true,
        ViewFolder: "Development",
        AssetFolder: "development");

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
