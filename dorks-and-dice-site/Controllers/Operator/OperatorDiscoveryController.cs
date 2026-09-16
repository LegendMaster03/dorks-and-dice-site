using System.Security.Claims;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
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
    public IActionResult OpenApi()
    {
        var paths = capabilities.GetSiteCapabilities()
            .GroupBy(capability => capability.Route, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (object)group.ToDictionary(
                    capability => capability.Method.ToLowerInvariant(),
                    capability => (object)new
                    {
                        operationId = capability.Name,
                        summary = capability.Description,
                        security = new[] { new Dictionary<string, string[]> { ["OperatorBearer"] = [] } }
                    },
                    StringComparer.Ordinal),
                StringComparer.Ordinal);

        return Ok(new
        {
            openapi = "3.1.0",
            info = new
            {
                title = "Dorks & Dice Operator API",
                version = "1.0"
            },
            paths,
            components = new
            {
                securitySchemes = new Dictionary<string, object>
                {
                    ["OperatorBearer"] = new
                    {
                        type = "http",
                        scheme = "bearer",
                        bearerFormat = "ddop_v1"
                    }
                }
            }
        });
    }

    private static IReadOnlyList<string> EffectiveGlobalRoles(ClaimsPrincipal principal) =>
        AccountRoleHierarchy.GlobalRoleNames
            .Where(role => AccountRoleHierarchy.PrincipalHasGlobalRole(principal, role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
}
