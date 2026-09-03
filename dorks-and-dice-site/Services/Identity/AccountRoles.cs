using System.Security.Claims;

namespace dorks_and_dice_site.Services.Identity;

public static class AccountRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string GlobalEditor = "Global Editor";
    public const string Dev = "Dev";

    public static IReadOnlyList<string> OwnerManaged { get; } = [Admin, Dev];
    public static IReadOnlyList<string> AdminManaged { get; } = [GlobalEditor];
    public static IReadOnlyList<string> UiAssignable { get; } = [Admin, GlobalEditor, Dev];
    public static IReadOnlyList<string> Privileged { get; } = [Admin, Dev];
    public static IReadOnlyList<string> TrustedPrivileged { get; } = [Owner, Admin, Dev];

    public static IReadOnlyList<string> InheritedGlobalRoles(string role) =>
        AccountRoleHierarchy.GetInheritedGlobalRoles(role);

    public static IReadOnlyList<ScopedEditorRoleDefinition> InheritedEditorRoles(string role) =>
        AccountRoleHierarchy.GetInheritedScopedRoles(role)
            .Where(node => string.Equals(node.ScopedRole, ScopedAccountRoles.Editor, StringComparison.Ordinal))
            .Select(node => SiteModeEditorRoles.All.Single(editorRole =>
                string.Equals(editorRole.Scope, node.Scope, StringComparison.Ordinal)))
            .ToArray();
}

public enum AccountRoleInheritanceNodeKind
{
    GlobalRole,
    ScopedRole
}

public sealed record AccountRoleInheritanceNode(
    string Key,
    string DisplayName,
    AccountRoleInheritanceNodeKind Kind,
    string? GlobalRole,
    string? Scope,
    string? ScopedRole,
    IReadOnlyList<AccountRoleInheritanceNode> Children);

public static class AccountRoleHierarchy
{
    private static readonly IReadOnlyDictionary<string, AccountRoleInheritanceNode> GlobalNodes = BuildGlobalNodes();

    public static IEnumerable<string> GlobalRoleNames => GlobalNodes.Keys;

    public static AccountRoleInheritanceNode GetGlobalRole(string role) =>
        GlobalNodes.TryGetValue(role, out var node)
            ? node
            : throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown global role.");

    public static IReadOnlyList<AccountRoleInheritanceNode> GetInheritedNodes(string role) =>
        Flatten(GetGlobalRole(role).Children);

    public static IReadOnlyList<string> GetInheritedGlobalRoles(string role) =>
        GetInheritedNodes(role)
            .Where(node => node.Kind == AccountRoleInheritanceNodeKind.GlobalRole && node.GlobalRole is not null)
            .Select(node => node.GlobalRole!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<AccountRoleInheritanceNode> GetInheritedScopedRoles(string role) =>
        GetInheritedNodes(role)
            .Where(node => node.Kind == AccountRoleInheritanceNodeKind.ScopedRole)
            .ToArray();

    public static bool InheritsGlobalRole(string sourceRole, string targetRole) =>
        GetInheritedGlobalRoles(sourceRole).Contains(targetRole, StringComparer.Ordinal);

    public static bool InheritsScopedRole(string sourceRole, string scope, string scopedRole) =>
        GetInheritedScopedRoles(sourceRole).Any(node =>
            string.Equals(node.Scope, scope, StringComparison.Ordinal)
            && string.Equals(node.ScopedRole, scopedRole, StringComparison.Ordinal));

    public static bool PrincipalHasGlobalRole(ClaimsPrincipal principal, string role) =>
        principal.IsInRole(role)
        || GlobalRoleNames.Any(sourceRole =>
            principal.IsInRole(sourceRole) && InheritsGlobalRole(sourceRole, role));

    public static bool PrincipalHasScopedRole(ClaimsPrincipal principal, string scope, string role)
    {
        if (principal.HasClaim(AccountClaimTypes.ScopedRole, $"{scope}:{role}"))
        {
            return true;
        }

        return GlobalRoleNames.Any(sourceRole =>
            principal.IsInRole(sourceRole) && InheritsScopedRole(sourceRole, scope, role));
    }

    public static IReadOnlyList<string> GetGlobalInheritanceSources(
        IEnumerable<string> directlyAssignedRoles,
        string targetRole) =>
        directlyAssignedRoles
            .Where(sourceRole => !string.Equals(sourceRole, targetRole, StringComparison.Ordinal))
            .Where(sourceRole => GlobalNodes.ContainsKey(sourceRole) && InheritsGlobalRole(sourceRole, targetRole))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<string> GetScopedInheritanceSources(
        IEnumerable<string> directlyAssignedRoles,
        string scope,
        string scopedRole) =>
        directlyAssignedRoles
            .Where(GlobalNodes.ContainsKey)
            .Where(sourceRole => InheritsScopedRole(sourceRole, scope, scopedRole))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, AccountRoleInheritanceNode> BuildGlobalNodes()
    {
        var editorChildren = SiteModeEditorRoles.All
            .Select(editorRole => new AccountRoleInheritanceNode(
                $"scoped:{editorRole.Scope}:{ScopedAccountRoles.Editor}",
                editorRole.RoleName,
                AccountRoleInheritanceNodeKind.ScopedRole,
                null,
                editorRole.Scope,
                ScopedAccountRoles.Editor,
                []))
            .ToArray();

        var globalEditor = new AccountRoleInheritanceNode(
            $"global:{AccountRoles.GlobalEditor}",
            AccountRoles.GlobalEditor,
            AccountRoleInheritanceNodeKind.GlobalRole,
            AccountRoles.GlobalEditor,
            null,
            null,
            editorChildren);

        var admin = new AccountRoleInheritanceNode(
            $"global:{AccountRoles.Admin}",
            AccountRoles.Admin,
            AccountRoleInheritanceNodeKind.GlobalRole,
            AccountRoles.Admin,
            null,
            null,
            [globalEditor]);

        var dev = new AccountRoleInheritanceNode(
            $"global:{AccountRoles.Dev}",
            AccountRoles.Dev,
            AccountRoleInheritanceNodeKind.GlobalRole,
            AccountRoles.Dev,
            null,
            null,
            []);

        var owner = new AccountRoleInheritanceNode(
            $"global:{AccountRoles.Owner}",
            AccountRoles.Owner,
            AccountRoleInheritanceNodeKind.GlobalRole,
            AccountRoles.Owner,
            null,
            null,
            [admin, dev]);

        return new Dictionary<string, AccountRoleInheritanceNode>(StringComparer.Ordinal)
        {
            [AccountRoles.Owner] = owner,
            [AccountRoles.Admin] = admin,
            [AccountRoles.GlobalEditor] = globalEditor,
            [AccountRoles.Dev] = dev
        };
    }

    private static IReadOnlyList<AccountRoleInheritanceNode> Flatten(
        IEnumerable<AccountRoleInheritanceNode> roots)
    {
        var result = new List<AccountRoleInheritanceNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(AccountRoleInheritanceNode node)
        {
            if (!visited.Add(node.Key))
            {
                return;
            }

            result.Add(node);
            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }

        foreach (var root in roots)
        {
            Visit(root);
        }

        return result;
    }
}
