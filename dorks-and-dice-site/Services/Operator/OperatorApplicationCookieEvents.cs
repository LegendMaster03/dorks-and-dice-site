using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorApplicationCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var credentialClaim = context.Principal?.FindFirst(OperatorClaimTypes.CredentialId)?.Value;
        var originalUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        // Preserve the normal ASP.NET Identity security-stamp behavior for every application
        // cookie, including human sessions.
        await SecurityStampValidator.ValidatePrincipalAsync(context);

        if (string.IsNullOrWhiteSpace(credentialClaim))
        {
            return;
        }

        if (!Guid.TryParse(credentialClaim, out var credentialId)
            || !Guid.TryParse(originalUserId, out var userId))
        {
            await RejectAsync(context);
            return;
        }

        // Resolve Operator state only for cookies explicitly created by browser bootstrap.
        // Ordinary human cookie validation never queries Operator credentials.
        var credentialService = context.HttpContext.RequestServices
            .GetRequiredService<IOperatorCredentialService>();
        if (!await credentialService.IsActiveForUserAsync(
                credentialId,
                userId,
                context.HttpContext.RequestAborted))
        {
            await RejectAsync(context);
            return;
        }

        var refreshedUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(
                refreshedUserId,
                userId.ToString("D"),
                StringComparison.OrdinalIgnoreCase)
            || context.Principal?.Identity is not ClaimsIdentity identity)
        {
            await RejectAsync(context);
            return;
        }

        // SecurityStampValidator may refresh the Identity principal. Reattach the bootstrap-only
        // credential binding so any renewed sliding cookie remains revocable by the same credential.
        if (!identity.HasClaim(OperatorClaimTypes.CredentialId, credentialId.ToString("D")))
        {
            identity.AddClaim(new Claim(
                OperatorClaimTypes.CredentialId,
                credentialId.ToString("D")));
            context.ShouldRenew = true;
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
