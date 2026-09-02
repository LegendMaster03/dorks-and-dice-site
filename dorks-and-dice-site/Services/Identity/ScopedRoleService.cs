using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Services.Identity;

public interface IScopedRoleService
{
    Task<IReadOnlySet<string>> GetRolesAsync(ApplicationUser user, string scope);
    Task<bool> HasRoleAsync(ApplicationUser user, string scope, string role);
    Task<IdentityResult> SetRoleAsync(ApplicationUser user, string scope, string role, bool enabled);
}

public sealed class ScopedRoleService : IScopedRoleService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ScopedRoleService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlySet<string>> GetRolesAsync(ApplicationUser user, string scope)
    {
        ValidateScope(scope);
        var prefix = $"{scope}:";
        var claims = await _userManager.GetClaimsAsync(user);
        return claims
            .Where(claim => claim.Type == AccountClaimTypes.ScopedRole
                && claim.Value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(claim => claim.Value[prefix.Length..])
            .Where(ScopedAccountRoles.All.Contains)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> HasRoleAsync(ApplicationUser user, string scope, string role)
    {
        ValidateScope(scope);
        ValidateRole(role);
        var claimValue = BuildClaimValue(scope, role);
        var claims = await _userManager.GetClaimsAsync(user);
        return claims.Any(claim => claim.Type == AccountClaimTypes.ScopedRole
            && string.Equals(claim.Value, claimValue, StringComparison.Ordinal));
    }

    public async Task<IdentityResult> SetRoleAsync(
        ApplicationUser user,
        string scope,
        string role,
        bool enabled)
    {
        ValidateScope(scope);
        ValidateRole(role);

        var claim = new Claim(AccountClaimTypes.ScopedRole, BuildClaimValue(scope, role));
        var currentlyEnabled = await HasRoleAsync(user, scope, role);
        if (currentlyEnabled == enabled)
        {
            return IdentityResult.Success;
        }

        var result = enabled
            ? await _userManager.AddClaimAsync(user, claim)
            : await _userManager.RemoveClaimAsync(user, claim);
        if (!result.Succeeded)
        {
            return result;
        }

        return await _userManager.UpdateSecurityStampAsync(user);
    }

    private static string BuildClaimValue(string scope, string role) => $"{scope}:{role}";

    private static void ValidateScope(string scope)
    {
        if (!AccountRoleScopes.All.Contains(scope, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown account role scope.");
        }
    }

    private static void ValidateRole(string role)
    {
        if (!ScopedAccountRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown scoped account role.");
        }
    }
}
