using dorks_and_dice_site.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Restricts ordinary interactive Identity sign-in to human accounts.
/// Operator service principals instead authenticate through their dedicated
/// credential and browser-bootstrap paths.
/// </summary>
public sealed class ApplicationSignInManager : SignInManager<ApplicationUser>
{
    public ApplicationSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(
            userManager,
            contextAccessor,
            claimsFactory,
            optionsAccessor,
            logger,
            schemes,
            confirmation)
    {
    }

    public override Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.AccountKind == AccountKind.Human
            ? base.CanSignInAsync(user)
            : Task.FromResult(false);
}
