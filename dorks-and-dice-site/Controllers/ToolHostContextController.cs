using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("tool-host")]
public sealed class ToolHostContextController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;

    public ToolHostContextController(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    [AllowAnonymous]
    [HttpGet("{slug}/context")]
    public async Task<IActionResult> GetContext(string slug, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        if (tool is null
            || !tool.Enabled
            || !ToolVisibility.IsVisibleInMode(tool, siteMode))
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        Response.Headers.CacheControl = "no-store";

        ToolHostUserContext? userContext = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                userContext = new ToolHostUserContext
                {
                    Id = userId,
                    DisplayName = User.FindFirstValue(AccountClaimTypes.DisplayName)
                        ?? User.Identity.Name
                        ?? string.Empty
                };
            }
        }

        return Ok(new ToolHostContext
        {
            ToolSlug = tool.Slug,
            SiteMode = SiteModeValue(siteMode),
            User = userContext
        });
    }

    private static string SiteModeValue(SiteMode siteMode) => siteMode switch
    {
        SiteMode.DorksAndDice => SiteModeValues.DorksAndDiceModeValue,
        SiteMode.Professional => SiteModeValues.ProfessionalModeValue,
        SiteMode.Development => SiteModeValues.DevelopmentModeValue,
        _ => SiteModeValues.UnassignedModeValue
    };
}
