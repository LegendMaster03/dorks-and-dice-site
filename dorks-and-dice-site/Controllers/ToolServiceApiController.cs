using System.Security.Claims;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Browser-facing public gateway for the headless Rules Core service.
///
/// Rules Core has a stable registration key but deliberately has no public Tool slug. Browser-hosted
/// applications still need a same-origin path to its stable public API. This gateway preserves the
/// normal Tool Host authentication/ticket contract while resolving Rules Core by stable key.
/// It is intentionally Rules Core-specific rather than a general browser gateway for headless
/// services; other services remain unreachable from browser Tool routes unless explicitly designed
/// and reviewed for that exposure.
/// </summary>
[Authorize]
public sealed class ToolServiceApiController(
    IToolRegistry toolRegistry,
    IToolHostAuthenticationContextFactory authenticationContextFactory,
    IToolProxyService toolProxyService) : ControllerBase
{
    private const string RulesCoreKey = "rules-core";

    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("~/tool-host/registrations/rules-core/api/upstream")]
    [Route("~/tool-host/registrations/rules-core/api/upstream/{**proxyPath}")]
    public Task<IActionResult> RulesCoreRegistrationUpstream(
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken) =>
        ProxyRulesCoreAsync(proxyPath, cancellationToken);

    /// <summary>
    /// Compatibility bridge for browser consumers that predate Rules Core becoming a headless
    /// service. Literal routing intentionally takes precedence over ToolHostApiController's
    /// application-slug route. New consumers should use the stable registration-key route above.
    /// </summary>
    [AllowAnonymous]
    [DisableFormValueModelBinding]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("~/tool-host/rules-core/api/upstream")]
    [Route("~/tool-host/rules-core/api/upstream/{**proxyPath}")]
    public Task<IActionResult> LegacyRulesCoreUpstream(
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken) =>
        ProxyRulesCoreAsync(proxyPath, cancellationToken);

    private async Task<IActionResult> ProxyRulesCoreAsync(
        string? proxyPath,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        var tool = await toolRegistry.GetByKeyAsync(RulesCoreKey, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        if (tool is null
            || tool.Kind != ToolKind.Service
            || !tool.Enabled
            || string.IsNullOrWhiteSpace(modeId)
            || !ToolVisibility.IsVisibleInMode(tool, modeId)
            || string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl))
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
            modeId,
            cancellationToken);
        if (authenticationContext is null)
        {
            return Forbid();
        }

        var ticket = ToolAuthenticationTickets.Issue(authenticationContext);
        var introspectionPath = $"/tool-host/registrations/{tool.Key}/api/introspect";

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
