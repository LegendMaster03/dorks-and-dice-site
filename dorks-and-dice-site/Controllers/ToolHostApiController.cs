using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
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

    public ToolHostApiController(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(string slug, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        if (tool is null
            || !tool.Enabled
            || !ToolVisibility.IsVisibleInMode(tool, siteMode))
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        Response.Headers.CacheControl = "no-store";

        return Ok(new ToolHostApiSession
        {
            ToolSlug = tool.Slug,
            SiteMode = SiteModeValues.ToModeValue(siteMode),
            User = new ToolHostUserContext
            {
                Id = userId,
                DisplayName = User.FindFirstValue(AccountClaimTypes.DisplayName)
                    ?? User.Identity?.Name
                    ?? string.Empty
            }
        });
    }
}
