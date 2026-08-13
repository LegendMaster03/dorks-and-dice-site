using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("articles")]
public class ArticlesController : Controller
{
    private readonly IContentCatalogService _contentCatalogService;
    private readonly ISiteModePresentationService _siteModePresentationService;

    public ArticlesController(
        IContentCatalogService contentCatalogService,
        ISiteModePresentationService siteModePresentationService)
    {
        _contentCatalogService = contentCatalogService;
        _siteModePresentationService = siteModePresentationService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var articles = (await _contentCatalogService.GetByContextAsync(
            ContentTags.Article,
            modeContext.SiteMode,
            modeContext.IncludeUnlistedArticles,
            cancellationToken)).ToList();

        var model = new ArticlesIndexViewModel
        {
            Articles = articles,
            Presentation = _siteModePresentationService.GetArticlesIndexPresentation(modeContext.SiteMode),
            SiteMode = modeContext.SiteMode,
            IsDevelopmentPreview = modeContext.IsDevelopmentPreview,
            IncludeUnlistedActive = modeContext.IncludeUnlistedArticles,
            Categories = articles
                .Select(article => article.GetCategory(ContentTags.Article))
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Tags = articles
                .SelectMany(article => article.PublicTags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return View(model);
    }
}
