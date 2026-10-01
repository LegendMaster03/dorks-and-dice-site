using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize]
public sealed class ToolPrivateTunnelApiController(
    IToolRegistry toolRegistry,
    IToolHostAuthenticationContextFactory authenticationContextFactory,
    IToolDelegationCapabilityService delegationCapabilities,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("/tool-host/{slug}/api/private-tunnel/{targetKey}/ticket")]
    public async Task<IActionResult> IssueBySlug(
        [FromRoute] string slug,
        [FromRoute] string targetKey,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var sourceTool = await toolRegistry.GetBySlugAsync(slug, cancellationToken);
        if (sourceTool is null || sourceTool.Kind != ToolKind.Application)
        {
            return Unauthorized();
        }

        return await IssueCoreAsync(sourceTool, targetKey, cancellationToken);
    }

    [AllowAnonymous]
    [HttpPost("/tool-host/registrations/{sourceKey}/api/private-tunnel/{targetKey}/ticket")]
    public async Task<IActionResult> IssueByKey(
        [FromRoute] string sourceKey,
        [FromRoute] string targetKey,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var sourceTool = await toolRegistry.GetByKeyAsync(sourceKey, cancellationToken);
        if (sourceTool is null)
        {
            return Unauthorized();
        }

        return await IssueCoreAsync(sourceTool, targetKey, cancellationToken);
    }

    private async Task<IActionResult> IssueCoreAsync(
        ToolRegistration sourceTool,
        string targetKey,
        CancellationToken cancellationToken)
    {
        if (!TryReadBearerToken(out var capability)
            || !delegationCapabilities.TryUse(
                sourceTool.Key,
                capability,
                out var sourceContext)
            || sourceContext is null)
        {
            return Unauthorized();
        }

        if (!IsSourceAvailable(sourceTool, sourceContext.SiteMode))
        {
            return Unauthorized();
        }

        if (!ToolPrivateTunnelPolicy.Allows(configuration, sourceTool.Key, targetKey))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var targetTool = await toolRegistry.GetByKeyAsync(targetKey, cancellationToken);
        if (targetTool is null
            || !targetTool.Enabled
            || !ToolVisibility.IsVisibleInMode(targetTool, sourceContext.SiteMode))
        {
            return NotFound();
        }

        var targetContext = await authenticationContextFactory.CreatePrivateTunnelAsync(
            targetTool,
            sourceContext,
            cancellationToken);
        if (targetContext is null)
        {
            return Unauthorized();
        }

        var targetTicket = ToolAuthenticationTickets.Issue(targetContext);
        return Ok(new ToolPrivateTunnelTicketResponse
        {
            TargetKey = targetTool.Key,
            Ticket = targetTicket,
            IntrospectionPath = AuthenticationIntrospectionPath(targetTool)
        });
    }

    private static bool IsSourceAvailable(ToolRegistration sourceTool, string siteMode) =>
        sourceTool.Enabled
        && ToolVisibility.IsVisibleInMode(sourceTool, siteMode)
        && ToolPrivateTunnelPolicy.IsSupportedSource(sourceTool);

    private static string AuthenticationIntrospectionPath(ToolRegistration tool) =>
        tool.Kind == ToolKind.Application && !string.IsNullOrWhiteSpace(tool.Slug)
            ? $"/tool-host/{tool.Slug}/api/introspect"
            : $"/tool-host/registrations/{tool.Key}/api/introspect";

    private bool TryReadBearerToken(out string token)
    {
        token = string.Empty;
        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorization[bearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
