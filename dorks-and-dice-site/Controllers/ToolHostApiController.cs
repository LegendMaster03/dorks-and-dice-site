using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Campaigns;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize]
[Route("tool-host/{slug}/api")]
public sealed class ToolHostApiController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ICampaignAccessStore _campaignAccessStore;
    private readonly IToolProxyService _toolProxyService;

    public ToolHostApiController(
        IToolRegistry toolRegistry,
        ICampaignAccessStore campaignAccessStore,
        IToolProxyService toolProxyService)
    {
        _toolRegistry = toolRegistry;
        _campaignAccessStore = campaignAccessStore;
        _toolProxyService = toolProxyService;
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(string slug, CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        Response.Headers.CacheControl = "no-store";

        return Ok(new ToolHostApiSession
        {
            ToolSlug = access.Tool!.Slug,
            SiteMode = HttpContext.GetSiteModeContext().ActiveModeId!,
            User = BuildUserContext(access.UserId!),
            GlobalRoles = BuildEffectiveGlobalRoles()
        });
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(string slug, CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        var campaigns = await _campaignAccessStore.GetCampaignsForUserAsync(
            access.UserId!,
            cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(campaigns);
    }

    [HttpGet("campaigns/{campaignId:guid}")]
    public async Task<IActionResult> Campaign(
        string slug,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveToolAndUserAsync(slug, cancellationToken);
        if (access.Result is not null)
        {
            return access.Result;
        }

        var campaign = await _campaignAccessStore.GetCampaignForUserAsync(
            campaignId,
            access.UserId!,
            cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(campaign);
    }

    /// <summary>
    /// Gateway for an Embedded Module to call its Tool backend. Anonymous requests are proxied only
    /// when the Tool is explicitly registered with AllowAnonymous=true, and receive no trusted
    /// identity headers. Authenticated requests retain the ticket/introspection contract.
    /// </summary>
    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("upstream")]
    [Route("upstream/{**proxyPath}")]
    public async Task<IActionResult> Upstream(
        string slug,
        string? proxyPath,
        CancellationToken cancellationToken)
    {
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null
            || tool.IntegrationType != ToolIntegrationType.EmbeddedModule
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

            await _toolProxyService.ProxyAsync(
                HttpContext,
                tool,
                upstreamPath,
                cancellationToken);
            return new EmptyResult();
        }

        var campaigns = await _campaignAccessStore.GetCampaignsForUserAsync(
            userId,
            cancellationToken);
        var authenticationContext = new ToolHostAuthenticationContext
        {
            ToolSlug = tool.Slug,
            SiteMode = HttpContext.GetSiteModeContext().ActiveModeId!,
            User = BuildUserContext(userId),
            GlobalRoles = BuildEffectiveGlobalRoles(),
            Campaigns = campaigns
        };
        var ticket = ToolAuthenticationTickets.Issue(authenticationContext);
        var introspectionPath = $"/tool-host/{tool.Slug}/api/introspect";

        await _toolProxyService.ProxyAuthenticatedAsync(
            HttpContext,
            tool,
            upstreamPath,
            ticket,
            introspectionPath,
            cancellationToken);
        return new EmptyResult();
    }

    /// <summary>
    /// Redeems a one-time authentication ticket presented by the Tool backend. This endpoint does
    /// not authenticate with the browser cookie; possession of the random server-injected ticket
    /// is the short-lived capability. Tickets are scoped to the registered Tool slug.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("introspect")]
    public IActionResult Introspect(string slug)
    {
        Response.Headers.CacheControl = "no-store";

        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var ticket = authorization[bearerPrefix.Length..].Trim();
        if (!ToolAuthenticationTickets.TryRedeem(slug, ticket, out var context)
            || context is null)
        {
            return Unauthorized();
        }

        return Ok(context);
    }

    private async Task<(ToolRegistration? Tool, string? UserId, IActionResult? Result)>
        ResolveToolAndUserAsync(string slug, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null)
        {
            return (null, null, NotFound());
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (tool, null, Challenge());
        }

        return (tool, userId, null);
    }

    private async Task<ToolRegistration?> ResolveAvailableToolAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        return tool is not null
            && tool.Enabled
            && ToolVisibility.IsVisibleInMode(tool, modeId)
            ? tool
            : null;
    }

    private IReadOnlyList<string> BuildEffectiveGlobalRoles() =>
        AccountRoleHierarchy.GlobalRoleNames
            .Where(role => AccountRoleHierarchy.PrincipalHasGlobalRole(User, role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

    private ToolHostUserContext BuildUserContext(string userId) => new()
    {
        Id = userId,
        DisplayName = User.FindFirstValue(AccountClaimTypes.DisplayName)
            ?? User.Identity?.Name
            ?? string.Empty
    };
}
