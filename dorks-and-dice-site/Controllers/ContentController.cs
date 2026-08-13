using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public sealed class ContentController : Controller
{
    private readonly IContentCatalogService _catalog;
    private readonly IContentBodyRenderer _bodyRenderer;

    public ContentController(IContentCatalogService catalog, IContentBodyRenderer bodyRenderer)
    {
        _catalog = catalog;
        _bodyRenderer = bodyRenderer;
    }

    [HttpGet("/resume/{slug}")]
    public Task<IActionResult> ResumeDetail(string slug, CancellationToken cancellationToken) =>
        RenderDetailAsync(slug, ContentTags.Project, allowExperienceFallback: true, cancellationToken);

    [HttpGet("/articles/{slug}")]
    public Task<IActionResult> ArticleDetail(string slug, CancellationToken cancellationToken) =>
        RenderDetailAsync(slug, ContentTags.Article, allowExperienceFallback: false, cancellationToken);

    private async Task<IActionResult> RenderDetailAsync(
        string slug,
        string requestedContext,
        bool allowExperienceFallback,
        CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var item = await _catalog.GetForDetailAsync(
            slug,
            modeContext.SiteMode,
            modeContext.IsDevelopmentPreview,
            cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var contextTag = requestedContext;
        if (!item.HasTag(contextTag))
        {
            if (allowExperienceFallback && item.HasTag(ContentTags.Experience))
            {
                contextTag = ContentTags.Experience;
            }
            else
            {
                return NotFound();
            }
        }

        if (!item.IsListed)
        {
            ViewData["Robots"] = "noindex, nofollow";
        }

        ViewData["MetaTitle"] = item.MetaTitle ?? item.Title;
        ViewData["MetaDescription"] = item.MetaDescription ?? item.Summary;
        if (!string.IsNullOrWhiteSpace(item.MetaImage))
        {
            ViewData["MetaImage"] = item.MetaImage;
        }

        var backLinks = BuildBackLinks(item, contextTag);
        var viewModel = new ContentDetailViewModel
        {
            Item = item,
            ContextTag = contextTag,
            RenderedBodyHtml = _bodyRenderer.Render(item.BodyFormat, item.Body),
            BackLinks = backLinks,
            IsDevelopmentVisibilityOverride = modeContext.IsDevelopmentPreview
                && !item.IsVisibleInMode(modeContext.SiteMode)
        };

        return View("~/Views/Content/Details.cshtml", viewModel);
    }

    private static List<ContentNavigationLink> BuildBackLinks(ContentItem item, string contextTag)
    {
        if (contextTag == ContentTags.Article)
        {
            return
            [
                new ContentNavigationLink { Text = "Back to articles", Href = "/articles" }
            ];
        }

        var links = new List<ContentNavigationLink>();
        if (item.HasTag(ContentTags.Project))
        {
            links.Add(new ContentNavigationLink { Text = "Back to projects", Href = "/resume#projects-section" });
        }
        if (item.HasTag(ContentTags.Experience))
        {
            links.Add(new ContentNavigationLink { Text = "Back to experience", Href = "/resume#experience-section" });
        }
        return links;
    }
}
