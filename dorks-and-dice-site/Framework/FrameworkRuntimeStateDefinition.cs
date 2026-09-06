using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Compatibility metadata for framework runtime states that still travel through the legacy
/// SiteMode enum during migration. Framework fallback is not a site mode. Synthetic modes are
/// explicit control-plane modes that participate in runtime mode behavior without joining the
/// normal hosted-mode registry.
/// </summary>
public record FrameworkRuntimeStateDefinition(
    string Id,
    string DisplayName,
    SiteMode LegacyMode,
    string ViewFolder,
    string AssetFolder);

/// <summary>
/// A framework-owned runtime mode. Synthetic modes have mode identity and presentation metadata,
/// but are not normal hosted site modes and therefore do not receive generated mode-scoped roles
/// or deployment host/source policy.
/// </summary>
public sealed record SyntheticSiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode LegacyMode,
    string ViewFolder,
    string AssetFolder)
    : FrameworkRuntimeStateDefinition(Id, DisplayName, LegacyMode, ViewFolder, AssetFolder);

public static class SyntheticSiteModes
{
    public static SyntheticSiteModeDefinition Development { get; } = new(
        Id: "development",
        DisplayName: "Development",
        LegacyMode: SiteMode.Development,
        ViewFolder: "Development",
        AssetFolder: "development");

    public static IReadOnlyList<SyntheticSiteModeDefinition> All { get; } =
    [
        Development
    ];
}

public static class FrameworkRuntimeStates
{
    public static FrameworkRuntimeStateDefinition Fallback { get; } = new(
        Id: "unassigned",
        DisplayName: "Unassigned",
        LegacyMode: SiteMode.Unassigned,
        ViewFolder: "Unassigned",
        AssetFolder: "unassigned");

    // Compatibility alias while Trusted Preview callers migrate to the synthetic-mode contract.
    public static SyntheticSiteModeDefinition TrustedPreview => SyntheticSiteModes.Development;

    public static IReadOnlyList<FrameworkRuntimeStateDefinition> All { get; } =
    [
        Fallback,
        SyntheticSiteModes.Development
    ];

    public static bool TryGetByLegacyMode(SiteMode mode, out FrameworkRuntimeStateDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }
}
