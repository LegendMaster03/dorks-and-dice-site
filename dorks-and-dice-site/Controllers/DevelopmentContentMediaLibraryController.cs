using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/media")]
[RequestSizeLimit(ContentInputPolicy.MaxAssetUploadBytes + 65_536)]
public sealed class DevelopmentContentMediaLibraryController : Controller
{
    private readonly IContentAssetService _assets;
    private readonly IContentSourceRegistry _sources;

    public DevelopmentContentMediaLibraryController(IContentAssetService assets, IContentSourceRegistry sources)
    {
        _assets = assets;
        _sources = sources;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? source, CancellationToken cancellationToken)
    {
        try
        {
            source = ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sources, source);
            return View("~/Views/ContentMediaLibrary/Index.cshtml", new ContentAssetLibraryViewModel
            {
                SourceKey = source,
                Sources = ContentAuthoringSourceAccess.GetCentralSources(_sources)
                    .Select(item => new ContentAuthoringSourceOption
                    {
                        Key = item.Key,
                        DisplayName = item.DisplayName
                    })
                    .ToList(),
                Assets = (await _assets.GetForSourceAsync(source, cancellationToken)).ToList(),
                RouteBase = "/development/media",
                IsCentralAuthoring = true
            });
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(string? source, IFormFile? file, CancellationToken cancellationToken)
    {
        try
        {
            source = ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sources, source);
            if (file is null)
            {
                TempData["ContentMediaError"] = "Choose an image or PDF to upload.";
                return Redirect($"/development/media?source={Uri.EscapeDataString(source)}");
            }

            await using var stream = file.OpenReadStream();
            var asset = await _assets.UploadAsync(
                source, file.FileName, file.ContentType, stream, file.Length, cancellationToken);
            TempData["ContentMediaSuccess"] = $"Stored '{asset.FileName}' in {source}.";
            return Redirect($"/development/media?source={Uri.EscapeDataString(source)}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("{assetKey}/replace")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(
        string assetKey,
        string? source,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        try
        {
            source = ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sources, source);
            if (file is null)
            {
                TempData["ContentMediaError"] = "Choose a replacement file.";
                return Redirect($"/development/media?source={Uri.EscapeDataString(source)}");
            }

            await using var stream = file.OpenReadStream();
            var asset = await ContentAssetReplacement.ReplaceAsync(
                _assets,
                _sources,
                source,
                assetKey,
                file.FileName,
                file.ContentType,
                stream,
                file.Length,
                cancellationToken);
            TempData["ContentMediaSuccess"] =
                $"Replaced '{asset.FileName}' in {source} without changing its stable media URL.";
            return Redirect($"/development/media?source={Uri.EscapeDataString(source)}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("{assetKey}/preview")]
    public async Task<IActionResult> Preview(string assetKey, string? source, CancellationToken cancellationToken)
    {
        try
        {
            source = ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sources, source);
            var asset = await _assets.GetFromSourceAsync(source, assetKey, cancellationToken);
            return asset is null ? NotFound() : File(asset.Data, asset.MediaType);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
