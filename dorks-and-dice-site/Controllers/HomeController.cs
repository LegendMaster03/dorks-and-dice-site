using dorks_and_dice_site.Models;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace dorks_and_dice_site.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISiteModeHomeService _siteModeHomeService;

        public HomeController(ISiteModeHomeService siteModeHomeService)
        {
            _siteModeHomeService = siteModeHomeService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var modeContext = HttpContext.GetSiteModeContext();
            if (modeContext.IsTrustedPreview && modeContext.ActiveMode is null)
            {
                return RouteResolutionIssue(
                    "Trusted Preview cannot resolve this shared route",
                    "This shared route is available to normal site modes, but Trusted Preview does not currently have a site mode selected.");
            }

            var home = await _siteModeHomeService.GetHomeAsync(modeContext, cancellationToken);
            if (home.ViewData is not null)
            {
                foreach (var (key, value) in home.ViewData)
                {
                    ViewData[key] = value;
                }
            }

            return home.Model is null
                ? View(home.ViewPath)
                : View(home.ViewPath, home.Model);
        }

        public IActionResult NotFoundPage()
        {
            var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var statusCode = reExecuteFeature?.OriginalStatusCode ?? StatusCodes.Status404NotFound;
            Response.StatusCode = statusCode;

            if (statusCode != StatusCodes.Status404NotFound)
            {
                return new EmptyResult();
            }

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

        private string GetCurrentReturnUrl()
        {
            return $"{Request.Path}{Request.QueryString}";
        }
    }
}
