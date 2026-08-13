using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

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
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        source ??= _sourceRegistry.AuthoringSourceKey;
        try
        {
            var assets = await _assets.GetForPageAsync(source, slug, cancellationToken);
            return View(new ContentAssetAuthoringViewModel
            {
                SourceKey = source,
                Slug = slug,
                Assets = assets.ToList()
            });
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(
        string slug,
        string source,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        if (file is null)
        {
            TempData["ContentMediaError"] = "Choose an image to upload.";
            return RedirectToAction(nameof(Index), new { slug, source });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var asset = await _assets.UploadAsync(
                source,
                slug,
                file.FileName,
                file.ContentType,
                stream,
                file.Length,
                cancellationToken);
            TempData["ContentMediaSuccess"] = $"Stored '{asset.FileName}'.";
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
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

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

    private bool IsDevelopmentPreview() => HttpContext.GetSiteModeContext().IsDevelopmentPreview;
}
