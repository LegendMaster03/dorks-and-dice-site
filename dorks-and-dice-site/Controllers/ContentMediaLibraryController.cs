using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("development/media")]
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
    public async Task<IActionResult> Index(string? source, CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview()) return NotFound();
        source ??= _sources.AuthoringSourceKey;
        return View(new ContentAssetLibraryViewModel
        {
            SourceKey = source,
            Sources = _sources.GetAllSources().Select(item => new ContentAuthoringSourceOption
            {
                Key = item.Key,
                DisplayName = item.DisplayName
            }).ToList(),
            Assets = (await _assets.GetForSourceAsync(source, cancellationToken)).ToList()
        });
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(string source, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview()) return NotFound();
        if (file is null)
        {
            TempData["ContentMediaError"] = "Choose an image to upload.";
            return RedirectToAction(nameof(Index), new { source });
        }
        try
        {
            await using var stream = file.OpenReadStream();
            var asset = await _assets.UploadAsync(
                source, file.FileName, file.ContentType, stream, file.Length, cancellationToken);
            TempData["ContentMediaSuccess"] = $"Stored '{asset.FileName}' in {source}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentMediaError"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { source });
    }

    [HttpGet("{assetKey}/preview")]
    public async Task<IActionResult> Preview(string assetKey, string source, CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview()) return NotFound();
        var asset = await _assets.GetFromSourceAsync(source, assetKey, cancellationToken);
        return asset is null ? NotFound() : File(asset.Data, asset.MediaType);
    }

    private bool IsDevelopmentPreview() => HttpContext.GetSiteModeContext().IsDevelopmentPreview;
}
