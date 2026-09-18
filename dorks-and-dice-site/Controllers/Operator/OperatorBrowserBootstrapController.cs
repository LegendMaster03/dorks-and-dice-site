using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers.Operator;

[AllowAnonymous]
public sealed class OperatorBrowserBootstrapController(
    IOperatorBrowserBootstrapService browserBootstrapService,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet("/operator/bootstrap")]
    public async Task<IActionResult> Consume(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        var result = await browserBootstrapService.ConsumeAsync(token, cancellationToken);
        if (result.InvocationId.HasValue)
        {
            Response.Headers["X-Dorks-Operator-Invocation-Id"] = result.InvocationId.Value.ToString("D");
        }

        if (!result.Succeeded
            || result.User is null
            || !result.CredentialId.HasValue)
        {
            return Unauthorized();
        }

        // Password and ordinary interactive login remain blocked by ApplicationSignInManager.
        // This explicit bootstrap creates the normal Identity application cookie and binds that
        // cookie to the specific Operator credential that authorized this bootstrap.
        await signInManager.SignInWithClaimsAsync(
            result.User,
            isPersistent: false,
            [
                new Claim(
                    OperatorClaimTypes.CredentialId,
                    result.CredentialId.Value.ToString("D"))
            ]);
        return LocalRedirect("/");
    }
}
