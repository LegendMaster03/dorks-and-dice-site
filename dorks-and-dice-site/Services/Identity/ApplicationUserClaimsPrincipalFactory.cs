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

        if (await _userManager.IsInRoleAsync(user, AccountRoles.Owner))
        {
            if (!identity.HasClaim(identity.RoleClaimType, AccountRoles.Admin))
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, AccountRoles.Admin));
            }

            if (!identity.HasClaim(identity.RoleClaimType, AccountRoles.Dev))
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, AccountRoles.Dev));
            }
        }

        // Global Editor is an explicit global content-authoring role. Represent its
        // inherited editor authority as scoped Editor claims so all existing editor
        // authorization paths continue to respect the selected site mode.
        if (await _userManager.IsInRoleAsync(user, AccountRoles.GlobalEditor))
        {
            foreach (var scope in AccountRoleScopes.All)
            {
                var value = $"{scope}:{ScopedAccountRoles.Editor}";
                if (!identity.HasClaim(AccountClaimTypes.ScopedRole, value))
                {
                    identity.AddClaim(new Claim(AccountClaimTypes.ScopedRole, value));
                }
            }
        }

        return identity;
    }
}
