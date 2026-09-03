using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Identity;

public static class ScopedAccountRoles
{
    public const string Editor = "Editor";

    public static IReadOnlyList<string> All { get; } = [Editor];
}

public sealed record ScopedEditorRoleDefinition(
    SiteMode SiteMode,
    string Scope,
    string DisplayName)
{
    public string RoleName => $"{DisplayName} {ScopedAccountRoles.Editor}";
}

public static class SiteModeEditorRoles
{
    public static IReadOnlyList<ScopedEditorRoleDefinition> All { get; } =
        Enum.GetValues<SiteMode>()
            .Where(SiteModeValues.IsEditorMode)
            .Select(mode => new ScopedEditorRoleDefinition(
                mode,
                SiteModeValues.ToModeValue(mode),
                SiteModeValues.ToDisplayName(mode)))
            .ToArray();

    public static bool TryGetByScope(string scope, out ScopedEditorRoleDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate =>
            string.Equals(candidate.Scope, scope, StringComparison.Ordinal));
        return definition is not null;
    }

    public static bool TryGetByMode(SiteMode siteMode, out ScopedEditorRoleDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.SiteMode == siteMode);
        return definition is not null;
    }
}

public static class AccountRoleScopes
{
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
