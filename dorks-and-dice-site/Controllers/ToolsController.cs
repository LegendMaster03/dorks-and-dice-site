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
        var tools = (await _toolRegistry.GetAllAsync(cancellationToken))
            .Where(tool => tool.Enabled)
            .ToArray();
        return View(tools);
    }

    [AllowAnonymous]
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        if (tool is null || !tool.Enabled)
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        return View(tool);
    }
}
