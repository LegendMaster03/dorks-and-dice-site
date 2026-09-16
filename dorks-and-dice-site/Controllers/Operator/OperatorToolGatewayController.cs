using System.Text.RegularExpressions;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Operator;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers.Operator;

[ApiController]
[Authorize(AuthenticationSchemes = OperatorAuthenticationDefaults.Scheme)]
[ServiceFilter(typeof(OperatorAuditFilter))]
[Route("operator/v1/tools")]
public sealed partial class OperatorToolGatewayController(
    IToolRegistry toolRegistry,
    IToolHostAuthenticationContextFactory contextFactory,
    IToolProxyService proxyService) : ControllerBase
{
    [HttpGet("{slug}/manifest")]
    [OperatorCapability("tools.manifest")]
    public async Task<IActionResult> Manifest(
        string slug,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(slug, cancellationToken);
        if (resolved.Result is not null)
        {
            return resolved.Result;
        }

        await ProxyAsync(
            resolved.Tool!,
            resolved.SiteMode!,
            resolved.Tool!.OperatorManifestPath!,
            cancellationToken);
        return new EmptyResult();
    }

    [HttpPost("{slug}/invoke/{capability}")]
    [OperatorCapability("tools.invoke")]
    public async Task<IActionResult> Invoke(
        string slug,
        string capability,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capability)
            || capability.Length > 128
            || !CapabilityRegex().IsMatch(capability))
        {
            return Problem(
                title: "Invalid Tool capability",
                detail: "Capability names may contain lowercase letters, numbers, dots, underscores, and hyphens.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resolved = await ResolveAsync(slug, cancellationToken);
        if (resolved.Result is not null)
        {
            return resolved.Result;
        }

        await ProxyAsync(
            resolved.Tool!,
            resolved.SiteMode!,
            $"/operator/v1/invoke/{Uri.EscapeDataString(capability)}",
            cancellationToken);
        return new EmptyResult();
    }

    private async Task ProxyAsync(
        ToolRegistration tool,
        string siteMode,
        string path,
        CancellationToken cancellationToken)
    {
        var authenticationContext = await contextFactory.CreateAsync(
            tool,
            User,
            siteMode,
            cancellationToken);
        if (authenticationContext is null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var ticket = ToolAuthenticationTickets.Issue(authenticationContext);
        var introspectionPath = $"/tool-host/{tool.Slug}/api/introspect";
        await proxyService.ProxyAuthenticatedAsync(
            HttpContext,
            tool,
            path,
            ticket,
            introspectionPath,
            cancellationToken);
    }

    private async Task<(ToolRegistration? Tool, string? SiteMode, IActionResult? Result)> ResolveAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var tool = await toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        if (tool is null
            || !tool.Enabled
            || string.IsNullOrWhiteSpace(modeId)
            || !ToolVisibility.IsVisibleInMode(tool, modeId))
        {
            return (null, null, NotFound());
        }

        if (ToolOperatorContractPolicy.GetUnsupportedReason(tool) is { } contractError)
        {
            return (
                tool,
                modeId,
                Problem(
                    title: "Unsupported Tool Operator contract",
                    detail: contractError,
                    statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        if (string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl))
        {
            return (tool, modeId, NotFound());
        }

        return (tool, modeId, null);
    }

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityRegex();
}
