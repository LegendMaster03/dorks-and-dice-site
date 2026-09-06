using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public abstract class ContentMediaAuthoringControllerBase : Controller
{
    private readonly IContentAssetService _assets;
    private readonly IContentSourceRegistry _sourceRegistry;
    private readonly IContentAuthoringService _authoringService;

    protected ContentMediaAuthoringControllerBase(
        IContentAssetService assets,
        IContentSourceRegistry sourceRegistry,
        IContentAuthoringService authoringService)
    {
        _assets = assets;
        _sourceRegistry = sourceRegistry;
        _authoringService = authoringService;
    }

    protected abstract bool IsCentralAuthoring { get; }
    protected string RouteBase => IsCentralAuthoring ? "/development/content" : "/editor/content";

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string slug,
        string? source,
        string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            source = ResolvePageSource(source);
            if (!await CanEditPageAsync(source, slug, cancellationToken))
            {
                return NotFound();
            }

            var localAssets = await _assets.GetForPageAsync(source, slug, cancellationToken);
            var dependencyKeys = await _assets.GetDependencyKeysAsync(source, slug, cancellationToken);
            var poolSources = GetPoolSources();
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
                foreach (var dependencySource in poolSources)
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

            return View("~/Views/ContentMediaAuthoring/Index.cshtml", new ContentAssetAuthoringViewModel
            {
                SourceKey = source,
                Slug = slug,
                Assets = assets,
                AvailableAssets = pool
                    .Where(asset => !dependencyKeys.Contains(asset.AssetKey))
                    .Take(48)
                    .ToList(),
                SearchQuery = q?.Trim() ?? string.Empty,
                RouteBase = RouteBase,
                IsCentralAuthoring = IsCentralAuthoring
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
            source = ResolvePageSource(source);
            assetSource = ResolvePoolSource(assetSource);
            if (!await CanEditPageAsync(source, slug, cancellationToken))
            {
                return NotFound();
            }

            await _assets.AttachAsync(source, slug, assetSource, assetKey, cancellationToken);
            TempData["ContentMediaSuccess"] = "Media dependency added.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }

        return Redirect($"{RouteBase}/{slug}/media?source={Uri.EscapeDataString(source)}");
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
            source = ResolvePageSource(source);
            if (!await CanEditPageAsync(source, slug, cancellationToken))
            {
                return NotFound();
            }

            await _assets.DetachAsync(source, slug, assetKey, cancellationToken);
            TempData["ContentMediaSuccess"] = "Media dependency removed.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }

        return Redirect($"{RouteBase}/{slug}/media?source={Uri.EscapeDataString(source)}");
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
            source = ResolvePoolSource(source);
            var asset = await _assets.GetFromSourceAsync(source, assetKey, cancellationToken);
            return asset is null ? NotFound() : File(asset.Data, asset.MediaType);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private IReadOnlyList<ContentSourceDefinition> GetPoolSources() =>
        IsCentralAuthoring
            ? ContentAuthoringSourceAccess.GetCentralSources(_sourceRegistry)
            : ContentAuthoringSourceAccess.GetModeEditorSources(
                _sourceRegistry,
                HttpContext.GetSiteModeContext());

    private string ResolvePageSource(string? source) =>
        IsCentralAuthoring
            ? ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sourceRegistry, source)
            : ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(
                _sourceRegistry,
                HttpContext.GetSiteModeContext(),
                source);

    private string ResolvePoolSource(string? source) =>
        IsCentralAuthoring
            ? ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sourceRegistry, source)
            : ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(
                _sourceRegistry,
                HttpContext.GetSiteModeContext(),
                source);

    private async Task<bool> CanEditPageAsync(
        string source,
        string slug,
        CancellationToken cancellationToken)
    {
        if (IsCentralAuthoring)
        {
            return true;
        }

        var model = await _authoringService.GetEditAsync(source, slug, cancellationToken);
        if (model is null)
        {
            return false;
        }

        var item = new ContentItem
        {
            VisibleInModes = model.Document.VisibleModesSelection.ToList()
        };
        return ContentAuthoringModeAccess.CanEditItem(
            User,
            item,
            HttpContext.GetSiteModeContext());
    }
}
