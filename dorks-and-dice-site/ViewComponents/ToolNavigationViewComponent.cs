using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.ViewComponents;

public sealed class ToolNavigationViewComponent(IToolRegistry toolRegistry) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated == true;
        var tools = (await toolRegistry.GetAllAsync(HttpContext.RequestAborted))
            .Where(tool => ToolVisibility.IsVisibleToUser(tool, modeId, isAuthenticated))
            .OrderBy(tool => tool.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return View(tools);
    }
}
