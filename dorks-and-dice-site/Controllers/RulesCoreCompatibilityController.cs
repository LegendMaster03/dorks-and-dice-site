using System.Security.Claims;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Preserves the historical Rules Core browser/API gateway while the rules-core registration
/// transitions from a public Embedded Module application to a headless service. This is a
/// backend compatibility alias only; it does not create a /tools/rules-core page or navigation
/// entry for the service registration.
/// </summary>
public sealed class RulesCoreCompatibilityController(
    IToolRegistry toolRegistry,
    IToolHostAuthenticationContextFactory authenticationContextFactory,
    IToolProxyService toolProxyService) : ControllerBase
{
    private const string RulesCoreKey = "rules-core";

    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("~/tool-host/rules-core/api/upstream")]
    [Route("~/tool-host/rules-core/api/upstream/{**proxyPath}")]
    public async Task<IActionResult> Upstream(
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken)
    {
        var tool = await toolRegistry.GetByKeyAsync(RulesCoreKey, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        if (tool is null
            || !tool.Enabled
            || !ToolVisibility.IsVisibleInMode(tool, modeId))
        {
            return NotFound();
        }

        if (tool.Kind == ToolKind.Application)
        {
            if (tool.IntegrationType != ToolIntegrationType.EmbeddedModule)
            {
                return NotFound();
            }

            if (ToolIntegrationContractPolicy.GetUnsupportedReason(tool) is { } contractError)
            {
                return Problem(
                    title: "Unsupported tool integration contract",
                    detail: contractError,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
        else if (tool.Kind != ToolKind.Service)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl))
        {
            return NotFound();
        }

        var upstreamPath = string.IsNullOrWhiteSpace(proxyPath) ? "/" : $"/{proxyPath}";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            if (User.Identity?.IsAuthenticated == true || !tool.AllowAnonymous)
            {
                return Challenge();
            }

            await toolProxyService.ProxyAsync(
                HttpContext,
                tool,
                upstreamPath,
                cancellationToken);
            return new EmptyResult();
        }

        var authenticationContext = await authenticationContextFactory.CreateAsync(
            tool,
            User,
            modeId!,
            cancellationToken);
        if (authenticationContext is null)
        {
            return Forbid();
        }

        var ticket = ToolAuthenticationTickets.Issue(authenticationContext);
        var introspectionPath = tool.Kind == ToolKind.Application
            && !string.IsNullOrWhiteSpace(tool.Slug)
                ? $"/tool-host/{tool.Slug}/api/introspect"
                : $"/tool-host/registrations/{tool.Key}/api/introspect";

        await toolProxyService.ProxyAuthenticatedAsync(
            HttpContext,
            tool,
            upstreamPath,
            ticket,
            introspectionPath,
            cancellationToken);
        return new EmptyResult();
    }
}
