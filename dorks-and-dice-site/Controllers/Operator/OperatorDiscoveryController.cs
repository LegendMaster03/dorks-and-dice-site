using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers.Operator;

[ApiController]
[Authorize(AuthenticationSchemes = OperatorAuthenticationDefaults.Scheme)]
[ServiceFilter(typeof(OperatorAuditFilter))]
[Route("operator/v1")]
public sealed class OperatorDiscoveryController(
    UserManager<ApplicationUser> userManager,
    IOperatorCapabilityRegistry capabilities,
    IOperatorBrowserBootstrapService browserBootstrapService) : ControllerBase
{
    [HttpGet("me")]
    [OperatorCapability("operator.me")]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null
            || user.DeletedAt is not null
            || user.AccountKind != AccountKind.ServicePrincipal)
        {
            return Unauthorized();
        }

        return Ok(new OperatorMeResponse(
            user.Id,
            user.DisplayName,
            user.AccountKind.ToString(),
            EffectiveGlobalRoles(User)));
    }

    [HttpGet("capabilities")]
    [OperatorCapability("operator.capabilities")]
    public IActionResult Capabilities() =>
        Ok(new OperatorCapabilitiesResponse(capabilities.GetSiteCapabilities()));

    [HttpPost("browser-bootstrap")]
    [OperatorCapability("operator.browser_bootstrap")]
    public async Task<IActionResult> BrowserBootstrap(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(User.FindFirstValue(OperatorClaimTypes.CredentialId), out var credentialId))
        {
            return Unauthorized();
        }

        var invocationId = Guid.TryParse(
            Response.Headers["X-Dorks-Operator-Invocation-Id"].FirstOrDefault(),
            out var parsedInvocationId)
                ? parsedInvocationId
                : Guid.NewGuid();

        var issued = await browserBootstrapService.IssueAsync(
            userId,
            credentialId,
            invocationId,
            cancellationToken);
        Response.Headers.CacheControl = "no-store";

        return Ok(new OperatorBrowserBootstrapResponse(
            issued.Bootstrap.Id,
            $"/operator/bootstrap?token={Uri.EscapeDataString(issued.Token)}",
            issued.Bootstrap.ExpiresAt));
    }

    [HttpGet("openapi.json")]
    [OperatorCapability("operator.openapi")]
    public IActionResult OpenApi() =>
        Ok(OperatorOpenApiDocument.Create(capabilities.GetSiteCapabilities()));

    private static IReadOnlyList<string> EffectiveGlobalRoles(ClaimsPrincipal principal) =>
        AccountRoleHierarchy.GlobalRoleNames
            .Where(role => AccountRoleHierarchy.PrincipalHasGlobalRole(principal, role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
}
