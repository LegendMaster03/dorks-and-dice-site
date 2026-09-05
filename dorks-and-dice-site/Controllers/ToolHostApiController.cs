using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Campaigns;
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

    public ToolHostApiController(
        IToolRegistry toolRegistry,
        ICampaignAccessStore campaignAccessStore)
    {
        _toolRegistry = toolRegistry;
        _campaignAccessStore = campaignAccessStore;
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
            SiteMode = SiteModeValues.ToModeValue(HttpContext.GetSiteModeContext().SiteMode),
            User = BuildUserContext(access.UserId!)
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

    private async Task<(Models.Tools.ToolRegistration? Tool, string? UserId, IActionResult? Result)>
        ResolveToolAndUserAsync(string slug, CancellationToken cancellationToken)
    {
        // Membership-dependent failures must not be cached either.
        Response.Headers.CacheControl = "no-store";
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        if (tool is null
            || !tool.Enabled
            || !ToolVisibility.IsVisibleInMode(tool, siteMode))
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

    private ToolHostUserContext BuildUserContext(string userId) => new()
    {
        Id = userId,
        DisplayName = User.FindFirstValue(AccountClaimTypes.DisplayName)
            ?? User.Identity?.Name
            ?? string.Empty
    };
}
