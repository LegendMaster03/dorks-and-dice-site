using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public sealed class DorksAndDiceDiscordLinkedAccountProjectionSource(
    IdentityDbContext identityDbContext,
    IModeExternalConnectionRegistry modeConnections) : IDiscordWorkspaceProjectionSource
{
    public const string ProjectionSourceId = "dorks-and-dice-linked-accounts";
    private const string ModeId = SiteModeValues.DorksAndDiceModeValue;

    // Keep the managed Discord object identity that was originally created while Rules Lawyer
    // was a global Site role. Reusing the key lets reconciliation rename and reassign that role
    // in place when its Site authority becomes Dorks & Dice-scoped instead of deleting and
    // recreating a Discord role that already exists in the live guild.
    private const string RulesLawyerDiscordRoleKey = "site-global-rules-lawyer";

    public string SourceId => ProjectionSourceId;

    public async Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        if (!modeConnections.TryGet(
                ModeId,
                DiscordProvider.Id,
                out var modeConnection)
            || modeConnection is null)
        {
            return [];
        }

        var userIds = await (
            from activation in identityDbContext.AccountLinkModeActivations
            join login in identityDbContext.UserLogins
                on activation.UserId equals login.UserId
            where activation.ModeId == ModeId
                && activation.ProviderId == DiscordProvider.Id
                && activation.ResourceId == modeConnection.ResourceId
                && login.LoginProvider == DiscordProvider.Id
            select activation.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var directGlobalRoles = await (
            from userRole in identityDbContext.UserRoles.AsNoTracking()
            join role in identityDbContext.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId) && role.Name != null
            select new { userRole.UserId, Role = role.Name! })
            .ToArrayAsync(cancellationToken);

        var directScopedRoles = await identityDbContext.UserClaims
            .AsNoTracking()
            .Where(claim => userIds.Contains(claim.UserId)
                && claim.ClaimType == AccountClaimTypes.ScopedRole
                && claim.ClaimValue != null)
            .Select(claim => new { claim.UserId, Role = claim.ClaimValue! })
            .ToArrayAsync(cancellationToken);

        var globalRolesByUser = directGlobalRoles.ToLookup(row => row.UserId, row => row.Role);
        var scopedRolesByUser = directScopedRoles.ToLookup(row => row.UserId, row => row.Role);
        var principals = userIds.ToDictionary(
            userId => userId,
            userId => BuildPrincipal(
                userId,
                globalRolesByUser[userId],
                scopedRolesByUser[userId]));

        var desiredRoles = new List<DiscordDesiredRole>
        {
            new(
                "linked-account",
                "Linked Account",
                userIds)
        };

        foreach (var globalRole in AccountRoleHierarchy.GlobalRoleNames
                     .OrderBy(role => role, StringComparer.Ordinal))
        {
            desiredRoles.Add(new DiscordDesiredRole(
                $"site-global-{NormalizeRoleKey(globalRole)}",
                globalRole,
                userIds
                    .Where(userId => AccountRoleHierarchy.PrincipalHasGlobalRole(
                        principals[userId],
                        globalRole))
                    .ToArray()));
        }

        foreach (var scopedRole in ScopedAccountRoles.ForScope(ModeId)
                     .OrderBy(role => role, StringComparer.Ordinal))
        {
            var roleKey = string.Equals(
                scopedRole,
                ScopedAccountRoles.RulesLawyer,
                StringComparison.Ordinal)
                    ? RulesLawyerDiscordRoleKey
                    : $"site-scoped-{NormalizeRoleKey(scopedRole)}";

            desiredRoles.Add(new DiscordDesiredRole(
                roleKey,
                $"{BuiltInSiteModes.DorksAndDice.DisplayName} {scopedRole}",
                userIds
                    .Where(userId => AccountRoleHierarchy.PrincipalHasScopedRole(
                        principals[userId],
                        ModeId,
                        scopedRole))
                    .ToArray()));
        }

        return
        [
            new DiscordGuildWorkspaceProjection(
                ModeId,
                modeConnection.ResourceId,
                modeConnection.ResourceId,
                desiredRoles,
                [])
        ];
    }

    private static ClaimsPrincipal BuildPrincipal(
        Guid userId,
        IEnumerable<string> globalRoles,
        IEnumerable<string> scopedRoles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString("D"))
        };
        claims.AddRange(globalRoles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(scopedRoles.Select(role => new Claim(AccountClaimTypes.ScopedRole, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "discord-role-projection"));
    }

    private static string NormalizeRoleKey(string role) =>
        role.Trim().ToLowerInvariant().Replace(' ', '-');
}
