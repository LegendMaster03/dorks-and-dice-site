using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Identity;

public sealed class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
        _userManager = userManager;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AccountClaimTypes.DisplayName, user.DisplayName));

        var directRoles = await _userManager.GetRolesAsync(user);

        // Materialize inherited Trusted Access roles that existing ASP.NET authorization
        // policies require as role claims. On public requests the claims transformation strips
        // those Trusted-only roles while preserving any non-privileged role they inherit, such
        // as Global Editor, so safe editor authority remains available without exposing Admin,
        // Owner, or Dev authority.
        foreach (var directRole in directRoles)
        {
            foreach (var inheritedRole in AccountRoleHierarchy.GetInheritedGlobalRoles(directRole))
            {
                if (AccountRoles.TrustedPrivileged.Contains(inheritedRole, StringComparer.Ordinal)
                    && !identity.HasClaim(identity.RoleClaimType, inheritedRole))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, inheritedRole));
                }
            }
        }

        // Directly assigned non-trusted global roles may safely materialize their
        // inherited scoped capabilities. The hierarchy is the source of truth.
        foreach (var directRole in directRoles.Where(role =>
                     !AccountRoles.TrustedPrivileged.Contains(role, StringComparer.Ordinal)))
        {
            foreach (var inheritedRole in AccountRoleHierarchy.GetInheritedScopedRoles(directRole))
            {
                if (inheritedRole.Scope is null || inheritedRole.ScopedRole is null)
                {
                    continue;
                }

                var value = $"{inheritedRole.Scope}:{inheritedRole.ScopedRole}";
                if (!identity.HasClaim(AccountClaimTypes.ScopedRole, value))
                {
                    identity.AddClaim(new Claim(AccountClaimTypes.ScopedRole, value));
                }
            }
        }

        return identity;
    }
}
