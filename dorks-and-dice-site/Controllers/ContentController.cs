using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public sealed class ContentController : Controller
{
    private readonly IContentCatalogService _catalog;
    private readonly IContentBodyRenderer _bodyRenderer;
    private readonly IContentRedirectService _redirects;
    private readonly IAuthorizationService _authorizationService;

    public ContentController(
        IContentCatalogService catalog,
        IContentBodyRenderer bodyRenderer,
        IContentRedirectService redirects,
        IAuthorizationService authorizationService)
    {
        _catalog = catalog;
        _bodyRenderer = bodyRenderer;
        _redirects = redirects;
        _authorizationService = authorizationService;
    }

    [HttpGet("/resume/{slug}")]
    public Task<IActionResult> ResumeDetail(string slug, CancellationToken cancellationToken)
    {
        var requestedContext = string.Equals(
            Request.Query["context"].FirstOrDefault(),
            ContentTags.Experience,
            StringComparison.OrdinalIgnoreCase)
            ? ContentTags.Experience
            : ContentTags.Project;

        return ResolveDetailAsync(
            slug,
            ContentRouteNamespaces.Resume,
            requestedContext,
            allowExperienceFallback: true,
            cancellationToken);
    }

    [HttpGet("/articles/{slug}")]
    public Task<IActionResult> ArticleDetail(string slug, CancellationToken cancellationToken) =>
        ResolveDetailAsync(
            slug,
            ContentRouteNamespaces.Articles,
            ContentTags.Article,
            allowExperienceFallback: false,
            cancellationToken);

    private async Task<IActionResult> ResolveDetailAsync(
        string slug,
        string routeNamespace,
        string requestedContext,
        bool allowExperienceFallback,
        CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var item = await _catalog.GetForDetailAsync(
            slug,
            modeContext,
            cancellationToken);

        if (item is null)
        {
            var redirect = await _redirects.ResolveAsync(routeNamespace, slug, cancellationToken);
            if (redirect is null)
            {
                return NotFound();
            }

            var targetItem = await _catalog.GetForDetailByIdAsync(
                redirect.ContentKey,
                modeContext,
                cancellationToken);
            if (targetItem is null
                || ResolveContextTag(targetItem, requestedContext, allowExperienceFallback) is null
                || string.Equals(targetItem.Slug, slug, StringComparison.Ordinal))
            {
                return NotFound();
            }

            return RedirectPermanent($"/{routeNamespace}/{targetItem.Slug}{Request.QueryString}");
        }

        var contextTag = ResolveContextTag(item, requestedContext, allowExperienceFallback);
        if (contextTag is null)
        {
            return NotFound();
        }

        if (!item.IsListed)
        {
            ViewData["Robots"] = "noindex, nofollow";
        }

        ViewData["MetaTitle"] = item.MetaTitle ?? item.GetTitle(contextTag);
        ViewData["MetaDescription"] = item.MetaDescription ?? item.GetSummary(contextTag);
        if (!string.IsNullOrWhiteSpace(item.MetaImage))
        {
            ViewData["MetaImage"] = item.MetaImage;
        }

        var canEdit = (await _authorizationService.AuthorizeAsync(
            User,
            AuthorizationPolicies.ModeEditor)).Succeeded;
        var backLinks = BuildBackLinks(item, contextTag);
        var viewModel = new ContentDetailViewModel
        {
            Item = item,
            ContextTag = contextTag,
            RenderedBodyHtml = _bodyRenderer.Render(item.BodyFormat, item.Body),
            BackLinks = backLinks,
            IsDevelopmentVisibilityOverride = modeContext.IsDevelopmentPreview
                && !item.IsVisibleInMode(modeContext.ActiveModeId),
            IsDevelopmentPreview = modeContext.IsDevelopmentPreview,
            EditHref = canEdit ? $"/editor/content/{item.Slug}/edit" : null
        };

        return View("~/Views/Content/Details.cshtml", viewModel);
    }

    private static string? ResolveContextTag(
        ContentItem item,
        string requestedContext,
        bool allowExperienceFallback)
    {
        if (item.HasTag(requestedContext))
        {
            return requestedContext;
        }

        return allowExperienceFallback && item.HasTag(ContentTags.Experience)
            ? ContentTags.Experience
            : null;
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
