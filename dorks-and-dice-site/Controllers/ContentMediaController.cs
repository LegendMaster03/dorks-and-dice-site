using dorks_and_dice_site.Models.Content;
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

        if (asset.MediaType == "image/svg+xml")
        {
            Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";

        var etag = $"\"sha256-{asset.Sha256}\"";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch.Any(header => header
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(candidate => candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal))))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(asset.Data, asset.MediaType);
    }
}
