using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public enum SiteModeKind
{
    Standard,
    Fallback,
    TrustedPreview
}

public sealed record SiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode? LegacyMode,
    string ViewFolder,
    string AssetFolder,
    SiteModeKind Kind = SiteModeKind.Standard)
{
    public bool IsStandardMode => Kind == SiteModeKind.Standard;
    public bool IsFallback => Kind == SiteModeKind.Fallback;
    public bool IsTrustedPreview => Kind == SiteModeKind.TrustedPreview;

    // Normal site modes participate in the shared content and scoped-capability model.
    // Fallback and Trusted Preview are framework/global-system concerns rather than
    // independently scoped content sites.
    public bool SupportsContent => IsStandardMode;
    public bool SupportsScopedEditor => IsStandardMode;
}

public static class BuiltInSiteModes
{
    // Unassigned is the framework fallback used when a mode-owned presentation or other
    // overridable behavior is unavailable. It is not a normal independently hosted site.
    public static SiteModeDefinition Unassigned { get; } = new(
        Id: "unassigned",
        DisplayName: "Unassigned",
        LegacyMode: SiteMode.Unassigned,
        ViewFolder: "Unassigned",
        AssetFolder: "unassigned",
        Kind: SiteModeKind.Fallback);

    // Dorks & Dice and Professional represent the normal site-mode shape. Future normal
    // modes inherit content and scoped-editor behavior without declaring those capabilities
    // individually.
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

    // Development is the legacy runtime name for Trusted Preview: a global administrative
    // and development surface for inspecting and previewing the composed system. It is not
    // a tenant/site mode and therefore does not receive mode-scoped content permissions.
    public static SiteModeDefinition Development { get; } = new(
        Id: "development",
        DisplayName: "Development",
        LegacyMode: SiteMode.Development,
        ViewFolder: "Development",
        AssetFolder: "development",
        Kind: SiteModeKind.TrustedPreview);

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
