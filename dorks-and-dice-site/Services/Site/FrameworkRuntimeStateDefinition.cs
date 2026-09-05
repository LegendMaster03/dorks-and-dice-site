using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Compatibility metadata for framework runtime states that still travel through the legacy
/// SiteMode enum during migration. These states are not normal hosted site modes and must not
/// be registered in ISiteModeRegistry.
/// </summary>
public sealed record FrameworkRuntimeStateDefinition(
    string Id,
    string DisplayName,
    SiteMode LegacyMode,
    string ViewFolder,
    string AssetFolder);

public static class FrameworkRuntimeStates
{
    public static FrameworkRuntimeStateDefinition Fallback { get; } = new(
        Id: "unassigned",
        DisplayName: "Unassigned",
        LegacyMode: SiteMode.Unassigned,
        ViewFolder: "Unassigned",
        AssetFolder: "unassigned");

    public static FrameworkRuntimeStateDefinition TrustedPreview { get; } = new(
        Id: "development",
        DisplayName: "Development",
        LegacyMode: SiteMode.Development,
        ViewFolder: "Development",
        AssetFolder: "development");

    public static IReadOnlyList<FrameworkRuntimeStateDefinition> All { get; } =
    [
        Fallback,
        TrustedPreview
    ];

    public static bool TryGetByLegacyMode(SiteMode mode, out FrameworkRuntimeStateDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }
}
