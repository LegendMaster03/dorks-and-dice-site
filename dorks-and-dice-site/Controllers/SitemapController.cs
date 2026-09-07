using System.Security;
using System.Text;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Generates the public sitemap from the active normal mode plus its current public content.
/// Database-backed content therefore becomes discoverable without rebuilding the application.
/// </summary>
public sealed class SitemapController : Controller
{
    private static readonly string[] PublicContextTags =
    [
        ContentTags.Homepage,
        ContentTags.Article,
        ContentTags.Project,
        ContentTags.Experience
    ];

    private readonly IContentCatalogService _catalog;
    private readonly SiteModeOptions _siteModeOptions;

    public SitemapController(IContentCatalogService catalog, SiteModeOptions siteModeOptions)
    {
        _catalog = catalog;
        _siteModeOptions = siteModeOptions;
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        if (modeContext.ActiveMode is null || modeContext.SyntheticMode is not null)
        {
            return NotFound();
        }

        var paths = new HashSet<string>(modeContext.ActiveMode.SitemapPaths, StringComparer.OrdinalIgnoreCase);
        var seenContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var contextTag in PublicContextTags)
        {
            var items = await _catalog.GetByContextAsync(
                contextTag,
                modeContext,
                includeUnlisted: false,
                cancellationToken);

            foreach (var item in items)
            {
                if (!item.IsListed || !seenContentIds.Add(item.Id))
                {
                    continue;
                }

                paths.Add(ContentPublicRoute.GetPath(item.Slug, item.Tags));
            }
        }

        var urls = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .OrderBy(path => path == "/" ? 0 : 1)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => $"<url><loc>{SecurityElement.Escape(BuildAbsoluteUrl(modeContext, path))}</loc></url>");

        var xml = new StringBuilder()
            .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
            .Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">")
            .AppendJoin(string.Empty, urls)
            .Append("</urlset>")
            .ToString();

        Response.Headers.CacheControl = "public, max-age=60";
        return Content(xml, "application/xml; charset=utf-8");
    }

    private string BuildAbsoluteUrl(SiteModeContext modeContext, string path)
    {
        var host = modeContext.ActiveModeId is { Length: > 0 } modeId
            && _siteModeOptions.TryGetCanonicalHost(modeId, out var canonicalHost)
                ? canonicalHost!
                : Request.Host.Value;
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        return $"{Request.Scheme}://{host}{pathBase}{path}";
    }
}
