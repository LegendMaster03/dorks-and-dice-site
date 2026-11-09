using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.ViewComponents;

/// <summary>
/// Provides Tool application navigation for the resolved normal mode and
/// current account. Registration visibility precedes menu rendering;
/// actual Tool execution has its own authoritative access middleware.
/// </summary>

public sealed class ToolNavigationViewComponent(IToolRegistry toolRegistry) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        var tools = (await toolRegistry.GetAllAsync(HttpContext.RequestAborted))
            .Where(tool => ToolVisibility.IsVisibleToUser(tool, modeId, HttpContext.User))
            .OrderBy(tool => tool.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return View(tools);
    }
}
