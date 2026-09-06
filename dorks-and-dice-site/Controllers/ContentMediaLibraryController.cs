using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.ModeEditor)]
[Route("editor/media")]
[RequestSizeLimit(ContentInputPolicy.MaxAssetUploadBytes + 65_536)]
public sealed class ContentMediaLibraryController : Controller
{
    private readonly IContentAssetService _assets;
    private readonly IContentSourceRegistry _sources;

    public ContentMediaLibraryController(IContentAssetService assets, IContentSourceRegistry sources)
    {
        _assets = assets;
        _sources = sources;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var source = _sources.AuthoringSourceKey;
        return View(new ContentAssetLibraryViewModel
        {
            SourceKey = source,
            Sources = [],
            Assets = (await _assets.GetForSourceAsync(source, cancellationToken)).ToList(),
            RouteBase = "/editor/media",
            IsCentralAuthoring = false
        });
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        var source = _sources.AuthoringSourceKey;
        if (file is null)
        {
            TempData["ContentMediaError"] = "Choose an image to upload.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var asset = await _assets.UploadAsync(
                source, file.FileName, file.ContentType, stream, file.Length, cancellationToken);
            TempData["ContentMediaSuccess"] = $"Stored '{asset.FileName}'.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{assetKey}/preview")]
    public async Task<IActionResult> Preview(string assetKey, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetFromSourceAsync(_sources.AuthoringSourceKey, assetKey, cancellationToken);
        return asset is null ? NotFound() : File(asset.Data, asset.MediaType);
    }
}
