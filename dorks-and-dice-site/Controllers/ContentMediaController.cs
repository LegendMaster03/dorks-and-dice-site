using dorks_and_dice_site.Services.Content;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("content/media")]
public sealed class ContentMediaController : Controller
{
    private readonly IContentAssetService _assets;

    public ContentMediaController(IContentAssetService assets)
    {
        _assets = assets;
    }

    [HttpGet("{assetKey}/{fileName}")]
    public async Task<IActionResult> Get(
        string assetKey,
        string fileName,
        CancellationToken cancellationToken)
    {
        ContentAssetFile? asset;
        try
        {
            asset = await _assets.GetForRequestAsync(assetKey, fileName, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        if (asset is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = $"\"sha256-{asset.Sha256}\"";
        return File(asset.Data, asset.MediaType);
    }
}
