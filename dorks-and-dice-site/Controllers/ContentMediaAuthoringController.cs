using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.ModeEditor)]
[Route("editor/content/{slug}/media")]
[Route("development/content/{slug}/media")]
[RequestSizeLimit(ContentInputPolicy.MaxAssetUploadBytes + 65_536)]
public sealed class ContentMediaAuthoringController : Controller
{
    private readonly IContentAssetService _assets;
    private readonly IContentSourceRegistry _sourceRegistry;

    public ContentMediaAuthoringController(
        IContentAssetService assets,
        IContentSourceRegistry sourceRegistry)
    {
        _assets = assets;
        _sourceRegistry = sourceRegistry;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string slug,
        string? source,
        string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            source = ResolveAccessibleSource(source);
            var localAssets = await _assets.GetForPageAsync(source, slug, cancellationToken);
            var dependencyKeys = await _assets.GetDependencyKeysAsync(source, slug, cancellationToken);
            var poolSources = ContentAuthoringSourceAccess
                .GetAccessibleSources(_sourceRegistry, HttpContext.GetSiteModeContext())
                .DistinctBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
            var pool = new List<ContentAssetInfo>();
            if (!string.IsNullOrWhiteSpace(q))
            {
                foreach (var poolSource in poolSources)
                {
                    pool.AddRange(await _assets.SearchSourceAsync(poolSource.Key, q, 24, cancellationToken));
                }
            }

            var assets = localAssets.ToList();
            foreach (var dependencyKey in dependencyKeys.Where(key =>
                         localAssets.All(local => local.AssetKey != key)))
            {
                foreach (var dependencySource in ContentAuthoringSourceAccess
                             .GetAccessibleSources(_sourceRegistry, HttpContext.GetSiteModeContext()))
                {
                    var dependencyAsset = await _assets.GetInfoFromSourceAsync(
                        dependencySource.Key, dependencyKey, cancellationToken);
                    if (dependencyAsset is null)
                    {
                        continue;
                    }

                    assets.Add(dependencyAsset);
                    break;
                }
            }

            return View(new ContentAssetAuthoringViewModel
            {
                SourceKey = source,
                Slug = slug,
                Assets = assets,
                AvailableAssets = pool
                    .Where(asset => !dependencyKeys.Contains(asset.AssetKey))
                    .Take(48)
                    .ToList(),
                SearchQuery = q?.Trim() ?? string.Empty
            });
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }
    }

    [HttpPost("attach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attach(
        string slug,
        string source,
        string assetSource,
        string assetKey,
        CancellationToken cancellationToken)
    {
        try
        {
            source = ResolveAccessibleSource(source);
            await _assets.AttachAsync(source, slug, assetSource, assetKey, cancellationToken);
            TempData["ContentMediaSuccess"] = "Media dependency added.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { slug, source });
    }

    [HttpPost("detach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Detach(
        string slug,
        string source,
        string assetKey,
        CancellationToken cancellationToken)
    {
        try
        {
            source = ResolveAccessibleSource(source);
            await _assets.DetachAsync(source, slug, assetKey, cancellationToken);
            TempData["ContentMediaSuccess"] = "Media dependency removed.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { slug, source });
    }

    [HttpGet("{assetKey}/preview")]
    public async Task<IActionResult> Preview(
        string slug,
        string assetKey,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            var asset = await _assets.GetFromSourceAsync(source, assetKey, cancellationToken);
            return asset is null ? NotFound() : File(asset.Data, asset.MediaType);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private string ResolveAccessibleSource(string? source) =>
        ContentAuthoringSourceAccess.ResolveAccessibleSourceKey(
            _sourceRegistry,
            HttpContext.GetSiteModeContext(),
            source);
}
