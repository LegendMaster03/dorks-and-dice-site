using Microsoft.AspNetCore.Mvc;
using dorks_and_dice_site.Services.Articles;

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
        return View(_articleCatalogService.GetIndex(IsProfessionalDomain()));
    }

    [HttpGet("freeing-the-bees-consolevariations-puzzle")]
    public IActionResult FreeingTheBeesConsoleVariationsPuzzle()
    {
        var article = _articleCatalogService.GetByAction(nameof(FreeingTheBeesConsoleVariationsPuzzle));
        if (IsProfessionalDomain() && article?.Professional != true)
        {
            return NotFound();
        }

        return View();
    }

    private bool IsProfessionalDomain()
    {
        return HttpContext.Items["ForceKyleBarnettBranding"] as bool? ?? false;
    }
}
