using dorks_and_dice_site.Models;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.GameServers.Minecraft;
using dorks_and_dice_site.Services.Resume;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace dorks_and_dice_site.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IMinecraftServerStatusService _minecraftServerStatusService;
        private readonly IResumeContentService _resumeContentService;

        public HomeController(
            IConfiguration configuration,
            IMinecraftServerStatusService minecraftServerStatusService,
            IResumeContentService resumeContentService)
        {
            _configuration = configuration;
            _minecraftServerStatusService = minecraftServerStatusService;
            _resumeContentService = resumeContentService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            switch (GetSiteMode())
            {
                case SiteMode.Professional:
                    return View(
                        "~/Views/SiteModes/Professional/Home.cshtml",
                        await _resumeContentService.GetResumePageAsync(cancellationToken));
                case SiteMode.Development:
                    return RouteResolutionIssue(
                        "Development mode cannot resolve this shared route",
                        "This shared route is available to multiple site modes, but it does not have explicit Development-mode handling.");
                case SiteMode.Unassigned:
                    return View("~/Views/SiteModes/Unassigned/Home.cshtml");
                case SiteMode.DorksAndDice:
                    ViewData["DiscordWidgetUrl"] = _configuration["Discord:WidgetUrl"];
                    ViewData["MinecraftServerStatus"] = await _minecraftServerStatusService.GetStatusAsync(cancellationToken);
                    return View("~/Views/SiteModes/DorksAndDice/Home.cshtml");
                default:
                    throw new InvalidOperationException($"Unhandled site mode: {GetSiteMode()}");
            }
        }

        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View();
        }

        public IActionResult RouteResolutionIssue()
        {
            return RouteResolutionIssue(
                HttpContext.Items[SiteModeContext.RouteResolutionTitleItemKey]?.ToString()
                    ?? "Route cannot be resolved",
                HttpContext.Items[SiteModeContext.RouteResolutionMessageItemKey]?.ToString()
                    ?? "The requested route cannot be resolved for the current site mode or domain.");
        }

        private IActionResult RouteResolutionIssue(string title, string message)
        {
            Response.StatusCode = 404;
            ViewData["RouteResolutionTitle"] = title;
            ViewData["RouteResolutionMessage"] = message;
            ViewData["DevelopmentPreviewReturnUrl"] = GetCurrentReturnUrl();
            return View("~/Views/Home/RouteResolutionIssue.cshtml");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private SiteMode GetSiteMode()
        {
            return HttpContext.GetSiteModeContext().SiteMode;
        }

        private string GetCurrentReturnUrl()
        {
            return $"{Request.Path}{Request.QueryString}";
        }
    }
}
