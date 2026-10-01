using System.Security.Claims;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Browser-facing public gateway for headless Tool services.
///
/// Services have stable registration keys but deliberately have no public Tool slug. Browser-hosted
/// applications still need a same-origin path to stable public service APIs such as Rules Core.
/// This gateway preserves the normal Tool Host authentication/ticket contract while resolving the
/// target by stable registration key instead of application slug.
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
    [Route("~/tool-host/registrations/{registrationKey}/api/upstream")]
    [Route("~/tool-host/registrations/{registrationKey}/api/upstream/{**proxyPath}")]
    public Task<IActionResult> RegistrationUpstream(
        [FromRoute] string registrationKey,
        [FromRoute] string? proxyPath,
        CancellationToken cancellationToken) =>
        ProxyServiceAsync(registrationKey, proxyPath, cancellationToken);

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
        ProxyServiceAsync(RulesCoreKey, proxyPath, cancellationToken);

    private async Task<IActionResult> ProxyServiceAsync(
        string registrationKey,
        string? proxyPath,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        var tool = await toolRegistry.GetByKeyAsync(registrationKey, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        if (tool is null
            || tool.Kind != ToolKind.Service
            || !tool.Enabled
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
