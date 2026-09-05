using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("tools")]
public sealed class ToolsController : Controller
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolProxyService _toolProxyService;

    public ToolsController(
        IToolRegistry toolRegistry,
        IToolProxyService toolProxyService)
    {
        _toolRegistry = toolRegistry;
        _toolProxyService = toolProxyService;
    }

    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        var tools = (await _toolRegistry.GetAllAsync(cancellationToken))
            .Where(tool => tool.Enabled && ToolVisibility.IsVisibleInMode(tool, siteMode))
            .ToArray();
        return View(tools);
    }

    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD")]
    [Route("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null)
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (tool.IntegrationType == ToolIntegrationType.ProxiedApplication)
        {
            await _toolProxyService.ProxyAsync(HttpContext, tool, "/", cancellationToken);
            return new EmptyResult();
        }

        return View(tool);
    }

    [AllowAnonymous]
    [AcceptVerbs("POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{slug}")]
    public Task<IActionResult> ProxyRoot(string slug, CancellationToken cancellationToken) =>
        ProxyResolvedAsync(slug, "/", cancellationToken);

    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{slug}/{**proxyPath}")]
    public Task<IActionResult> Proxy(
        string slug,
        string? proxyPath,
        CancellationToken cancellationToken) =>
        ProxyResolvedAsync(
            slug,
            string.IsNullOrWhiteSpace(proxyPath) ? "/" : $"/{proxyPath}",
            cancellationToken);

    private async Task<IActionResult> ProxyResolvedAsync(
        string slug,
        string path,
        CancellationToken cancellationToken)
    {
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null || tool.IntegrationType != ToolIntegrationType.ProxiedApplication)
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        await _toolProxyService.ProxyAsync(HttpContext, tool, path, cancellationToken);
        return new EmptyResult();
    }

    private async Task<ToolRegistration?> ResolveAvailableToolAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        return tool is not null
            && tool.Enabled
            && ToolVisibility.IsVisibleInMode(tool, siteMode)
            ? tool
            : null;
    }
}
