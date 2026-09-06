using System.Text;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Public text representations of the active normal site mode. These endpoints deliberately use
/// the normal content catalog and request mode context so they inherit the same source precedence,
/// visibility, and listing rules as the public site.
/// </summary>
public sealed partial class SiteTextController : Controller
{
    private readonly IContentCatalogService _catalog;

    public SiteTextController(IContentCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("/site.txt")]
    public async Task<IActionResult> Site(CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        if (modeContext.ActiveMode is null || modeContext.SyntheticMode is not null)
        {
            return NotFound();
        }

        var items = await GetPublicItemsAsync(modeContext, cancellationToken);
        var output = new StringBuilder();
        output.AppendLine($"# {modeContext.ActiveMode.DisplayName}");
        output.AppendLine($"Canonical site: {BuildAbsoluteUrl("/")}");
        output.AppendLine();
        output.AppendLine("This document is the current public text representation of this site mode.");

        foreach (var item in items)
        {
            var path = ContentPublicRoute.GetPath(item.Slug, item.Tags);
            output.AppendLine();
            output.AppendLine($"## {item.Title}");
            output.AppendLine($"URL: {BuildAbsoluteUrl(path)}");
            if (!string.IsNullOrWhiteSpace(item.Subtitle))
            {
                output.AppendLine($"Subtitle: {item.Subtitle}");
            }
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                output.AppendLine();
                output.AppendLine(item.Summary.Trim());
            }
            if (item.PublicTags.Count > 0)
            {
                output.AppendLine();
                output.AppendLine($"Tags: {string.Join(", ", item.PublicTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))}");
            }
            if (!string.IsNullOrWhiteSpace(item.Body))
            {
                output.AppendLine();
                output.AppendLine(NormalizeBodyForText(item.Body).Trim());
            }
        }

        Response.Headers.CacheControl = "public, max-age=60";
        return Content(output.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("/llms.txt")]
    public async Task<IActionResult> Llms(CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        if (modeContext.ActiveMode is null || modeContext.SyntheticMode is not null)
        {
            return NotFound();
        }

        var items = await GetPublicItemsAsync(modeContext, cancellationToken);
        var output = new StringBuilder();
        output.AppendLine($"# {modeContext.ActiveMode.DisplayName}");
        output.AppendLine();
        output.AppendLine($"Full public text: {BuildAbsoluteUrl("/site.txt")}");
        output.AppendLine();
        output.AppendLine("## Public pages");

        foreach (var item in items)
        {
            var path = ContentPublicRoute.GetPath(item.Slug, item.Tags);
            output.Append("- ").Append(item.Title).Append(": ").Append(BuildAbsoluteUrl(path));
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                output.Append(" — ").Append(CollapseWhitespace(item.Summary));
            }
            output.AppendLine();
        }

        Response.Headers.CacheControl = "public, max-age=60";
        return Content(output.ToString(), "text/plain; charset=utf-8");
    }

    private async Task<IReadOnlyList<ContentItem>> GetPublicItemsAsync(
        SiteModeContext modeContext,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ContentItem>();
        foreach (var tag in new[]
                 {
                     ContentTags.Homepage,
                     ContentTags.Article,
                     ContentTags.Project,
                     ContentTags.Experience
                 })
        {
            candidates.AddRange(await _catalog.GetByContextAsync(
                tag,
                modeContext,
                includeUnlisted: false,
                cancellationToken));
        }

        return candidates
            .Where(item => item.IsListed)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.HasTag(ContentTags.Homepage))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildAbsoluteUrl(string path)
    {
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        return $"{Request.Scheme}://{Request.Host}{pathBase}{path}";
    }

    private static string NormalizeBodyForText(string body)
    {
        var normalized = DynamicComponentRegex().Replace(body, match =>
        {
            var component = match.Groups["component"].Value;
            if (component.StartsWith("minecraft-server-", StringComparison.OrdinalIgnoreCase))
            {
                return "[Live Minecraft server information is available on the web page.]";
            }
            if (string.Equals(component, "discord-widget", StringComparison.OrdinalIgnoreCase))
            {
                return "[A Discord server widget is available on the web page.]";
            }
            if (string.Equals(component, "content-collection", StringComparison.OrdinalIgnoreCase))
            {
                return "[This page includes a public content collection. Its records are listed separately in this document.]";
            }

            return "[Dynamic page component available on the web page.]";
        });

        return normalized;
    }

    private static string CollapseWhitespace(string value) =>
        WhitespaceRegex().Replace(value.Trim(), " ");

    [GeneratedRegex(@"\{\{\s*(?<component>[a-z0-9-]+)(?:\s+[^}]*)?\}\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicComponentRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
