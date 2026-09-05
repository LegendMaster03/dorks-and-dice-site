using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("tools")]
public sealed class ToolsController : Controller
{
    private readonly IToolRegistry _toolRegistry;

    public ToolsController(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _toolRegistry = new JsonToolRegistry(environment, configuration);
    }

    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var modeValue = GetCurrentModeValue();
        var tools = (await _toolRegistry.GetAllAsync(cancellationToken))
            .Where(tool => tool.Enabled && IsVisibleInMode(tool.Modes, modeValue))
            .ToArray();
        return View(tools);
    }

    [AllowAnonymous]
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var modeValue = GetCurrentModeValue();
        if (tool is null || !tool.Enabled || !IsVisibleInMode(tool.Modes, modeValue))
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        return View(tool);
    }

    private string? GetCurrentModeValue() => HttpContext.GetSiteModeContext().SiteMode switch
    {
        SiteMode.DorksAndDice => SiteModeValues.DorksAndDiceModeValue,
        SiteMode.Professional => SiteModeValues.ProfessionalModeValue,
        _ => null
    };

    private static bool IsVisibleInMode(IReadOnlyCollection<string>? modes, string? modeValue)
    {
        if (modeValue is null)
        {
            return false;
        }

        // Registrations created before mode selection existed were Dorks & Dice-only.
        if (modes is null || modes.Count == 0)
        {
            return string.Equals(modeValue, SiteModeValues.DorksAndDiceModeValue, StringComparison.Ordinal);
        }

        return modes.Contains(modeValue, StringComparer.Ordinal);
    }
}
