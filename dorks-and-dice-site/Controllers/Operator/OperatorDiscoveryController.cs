using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using dorks_and_dice_site.Services.Tools;
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
    IToolRegistry toolRegistry) : ControllerBase
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
    public async Task<IActionResult> Capabilities(CancellationToken cancellationToken)
    {
        var tools = (await toolRegistry.GetAllAsync(cancellationToken))
            .Where(tool => tool.Enabled
                && tool.OperatorContractVersion == ToolOperatorContractVersions.Current
                && !string.IsNullOrWhiteSpace(tool.OperatorManifestPath))
            .Select(tool => new OperatorToolSummary(
                tool.Slug,
                tool.DisplayName,
                tool.OperatorContractVersion!.Value,
                tool.OperatorManifestPath!))
            .OrderBy(tool => tool.Slug, StringComparer.Ordinal)
            .ToArray();

        return Ok(new OperatorCapabilitiesResponse(
            capabilities.GetSiteCapabilities(),
            tools));
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
