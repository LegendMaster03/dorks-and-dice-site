using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Identity;

public static class ScopedAccountRoles
{
    public const string Editor = "Editor";

    public static IReadOnlyList<string> All { get; } = [Editor];
}

public sealed record ScopedEditorRoleDefinition(
    string Scope,
    string DisplayName,
    SiteMode? LegacySiteMode)
{
    public string RoleName => $"{DisplayName} {ScopedAccountRoles.Editor}";

    // Compatibility bridge for consumers that still operate on the legacy SiteMode enum.
    // New registry-driven consumers should use Scope instead.
    public SiteMode SiteMode => LegacySiteMode
        ?? throw new InvalidOperationException(
            $"Mode '{Scope}' does not have a legacy SiteMode enum value.");
}

public static class SiteModeEditorRoleFactory
{
    public static IReadOnlyList<ScopedEditorRoleDefinition> Create(
        IEnumerable<SiteModeDefinition> modes) =>
        modes
            .Where(mode => mode.SupportsScopedEditor)
            .Select(mode => new ScopedEditorRoleDefinition(
                mode.Id,
                mode.DisplayName,
                mode.LegacyMode))
            .ToArray();
}

public static class SiteModeEditorRoles
{
    public static IReadOnlyList<ScopedEditorRoleDefinition> All { get; } =
        SiteModeEditorRoleFactory.Create(BuiltInSiteModes.All);

    public static bool TryGetByScope(string scope, out ScopedEditorRoleDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate =>
            string.Equals(candidate.Scope, scope, StringComparison.Ordinal));
        return definition is not null;
    }

    public static bool TryGetByMode(SiteMode siteMode, out ScopedEditorRoleDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacySiteMode == siteMode);
        return definition is not null;
    }
}

public static class AccountRoleScopes
{
    // Compatibility constants for current callers. New mode-aware code should consume
    // registered mode ids rather than adding another constant here.
    public const string DorksAndDice = SiteModeValues.DorksAndDiceModeValue;
    public const string Professional = SiteModeValues.ProfessionalModeValue;

    public static IReadOnlyList<string> All { get; } =
        SiteModeEditorRoles.All.Select(role => role.Scope).ToArray();

    public static bool TryGetScope(SiteMode siteMode, out string? scope)
    {
        if (SiteModeEditorRoles.TryGetByMode(siteMode, out var definition))
        {
            scope = definition!.Scope;
            return true;
        }

        scope = null;
        return false;
    }

    public static string GetDisplayName(string scope) =>
        SiteModeEditorRoles.TryGetByScope(scope, out var definition)
            ? definition!.DisplayName
            : scope;
}
