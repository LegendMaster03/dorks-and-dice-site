using Microsoft.AspNetCore.Mvc;
using dorks_and_dice_site.Services.Articles;
using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Controllers;

[Route("articles")]
public class ArticlesController : Controller
{
    private readonly IArticleCatalogService _articleCatalogService;

    public ArticlesController(IArticleCatalogService articleCatalogService)
    {
        _articleCatalogService = articleCatalogService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(_articleCatalogService.GetIndex(
            GetSiteMode(),
            IncludeUnlistedArticles(),
            IsDevelopmentPreview()));
    }

    [HttpGet("freeing-the-bees-consolevariations-puzzle")]
    public IActionResult FreeingTheBeesConsoleVariationsPuzzle()
    {
        var article = _articleCatalogService.GetByAction(nameof(FreeingTheBeesConsoleVariationsPuzzle));
        if (article is null || !article.IsVisibleInMode(GetSiteMode()))
        {
            return NotFound();
        }

        if (!article.Listed)
        {
            ViewData["Robots"] = "noindex, nofollow";
        }

        ViewData["ArticleStatusLabel"] = article.Listed ? "Posted" : "Status";
        ViewData["ArticleStatusText"] = article.ListingStatusText;

        return View();
    }

    private SiteMode GetSiteMode()
    {
        return HttpContext.GetSiteModeContext().SiteMode;
    }

    private bool IncludeUnlistedArticles()
    {
        return HttpContext.GetSiteModeContext().IncludeUnlistedArticles;
    }

    private bool IsDevelopmentPreview()
    {
        return HttpContext.GetSiteModeContext().IsDevelopmentPreview;
    }

}
