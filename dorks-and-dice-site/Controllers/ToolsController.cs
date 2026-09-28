using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("tools")]
public sealed class ToolsController : Controller
{
    private const string RulesCoreKey = "rules-core";
    private const string RulesWikiSlug = "rules-wiki";

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
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        var tools = (await _toolRegistry.GetAllAsync(cancellationToken))
            .Where(tool => ToolVisibility.IsVisibleToUser(
                tool,
                modeId,
                User.Identity?.IsAuthenticated == true))
            .ToArray();
        return View(tools);
    }

    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD")]
    [Route("{slug}")]
    public Task<IActionResult> Details(string slug, CancellationToken cancellationToken) =>
        DispatchAsync(slug, "/", canonicalizeProxiedRoot: true, cancellationToken);

    [AllowAnonymous]
    [AcceptVerbs("POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{slug}")]
    public Task<IActionResult> RootRequest(string slug, CancellationToken cancellationToken) =>
        DispatchAsync(slug, "/", canonicalizeProxiedRoot: false, cancellationToken);

    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")]
    [Route("{slug}/{**toolRoute}")]
    public Task<IActionResult> RoutedRequest(
        string slug,
        string? toolRoute,
        CancellationToken cancellationToken) =>
        DispatchAsync(
            slug,
            string.IsNullOrWhiteSpace(toolRoute) ? "/" : $"/{toolRoute}",
            canonicalizeProxiedRoot: false,
            cancellationToken);

    private async Task<IActionResult> DispatchAsync(
        string slug,
        string path,
        bool canonicalizeProxiedRoot,
        CancellationToken cancellationToken)
    {
        var tool = await ResolveAvailableToolAsync(slug, cancellationToken);
        if (tool is null)
        {
            var compatibilityRedirect = await TryRedirectLegacyRulesCoreRouteAsync(
                slug,
                path,
                cancellationToken);
            return compatibilityRedirect ?? NotFound();
        }

        if (ToolIntegrationContractPolicy.GetUnsupportedReason(tool) is { } contractError)
        {
            return Problem(
                title: "Unsupported tool integration contract",
                detail: contractError,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (tool.IntegrationType == ToolIntegrationType.EmbeddedModule)
        {
            if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
            {
                return NotFound();
            }

            return RenderEmbeddedTool(tool, path);
        }

        if (tool.IntegrationType != ToolIntegrationType.ProxiedApplication)
        {
            return NotFound();
        }

        if (canonicalizeProxiedRoot)
        {
            var requestPath = Request.Path.Value ?? string.Empty;
            if (!requestPath.EndsWith("/", StringComparison.Ordinal))
            {
                return RedirectPreserveMethod($"/tools/{tool.Slug}/{Request.QueryString}");
            }
        }

        await _toolProxyService.ProxyAsync(HttpContext, tool, path, cancellationToken);
        return new EmptyResult();
    }

    private async Task<IActionResult?> TryRedirectLegacyRulesCoreRouteAsync(
        string requestedSlug,
        string path,
        CancellationToken cancellationToken)
    {
        if ((!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
            || !string.Equals(requestedSlug, RulesCoreKey, StringComparison.Ordinal))
        {
            return null;
        }

        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        var rulesCore = await _toolRegistry.GetByKeyAsync(RulesCoreKey, cancellationToken);
        if (rulesCore is null
            || rulesCore.Kind != ToolKind.Service
            || !rulesCore.Enabled
            || !ToolVisibility.IsVisibleInMode(rulesCore, modeId))
        {
            return null;
        }

        var rulesWiki = await ResolveAvailableToolAsync(RulesWikiSlug, cancellationToken);
        if (rulesWiki is null)
        {
            return null;
        }

        var suffix = string.Equals(path, "/", StringComparison.Ordinal) ? string.Empty : path;
        return RedirectPermanentPreserveMethod(
            $"/tools/{rulesWiki.Slug}{suffix}{Request.QueryString}");
    }

    private IActionResult RenderEmbeddedTool(ToolRegistration tool, string toolRoute)
    {
        var toolBasePath = $"/tools/{tool.Slug}";
        var contextUrl = $"/tool-host/{tool.Slug}/context";
        if (!string.Equals(toolRoute, "/", StringComparison.Ordinal))
        {
            contextUrl += $"?toolRoute={Uri.EscapeDataString(toolRoute)}";
        }

        ViewData["ToolBasePath"] = toolBasePath;
        ViewData["ToolRoute"] = toolRoute;
        ViewData["ToolContextUrl"] = contextUrl;
        return View("Details", tool);
    }

    private async Task<ToolRegistration?> ResolveAvailableToolAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var modeId = HttpContext.GetSiteModeContext().ActiveModeId;
        return tool is not null
            && tool.Kind == ToolKind.Application
            && !string.IsNullOrWhiteSpace(tool.Slug)
            && tool.Enabled
            && ToolVisibility.IsVisibleInMode(tool, modeId)
            ? tool
            : null;
    }
}
