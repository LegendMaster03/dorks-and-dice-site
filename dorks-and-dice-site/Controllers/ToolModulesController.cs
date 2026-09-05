using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("tool-modules")]
public sealed class ToolModulesController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IToolUpstreamPolicy _upstreamPolicy;

    public ToolModulesController(
        IToolRegistry toolRegistry,
        IHttpClientFactory httpClientFactory,
        IToolUpstreamPolicy upstreamPolicy)
    {
        _toolRegistry = toolRegistry;
        _httpClientFactory = httpClientFactory;
        _upstreamPolicy = upstreamPolicy;
    }

    [AllowAnonymous]
    [AcceptVerbs("GET", "HEAD")]
    [Route("{slug}/{**assetPath}")]
    public async Task<IActionResult> Forward(
        string slug,
        string? assetPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return NotFound();
        }

        var tool = await _toolRegistry.GetBySlugAsync(slug, cancellationToken);
        var siteMode = HttpContext.GetSiteModeContext().SiteMode;
        if (tool is null
            || !tool.Enabled
            || tool.IntegrationType != ToolIntegrationType.EmbeddedModule
            || !ToolVisibility.IsVisibleInMode(tool, siteMode))
        {
            return NotFound();
        }

        if (!tool.AllowAnonymous && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (!_upstreamPolicy.TryBuild(
                tool,
                $"/{assetPath}",
                Request.QueryString,
                out var upstreamUri,
                out _)
            || upstreamUri is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        try
        {
            var method = HttpMethods.IsHead(Request.Method) ? HttpMethod.Head : HttpMethod.Get;
            using var upstreamRequest = new HttpRequestMessage(method, upstreamUri);
            var acceptHeader = Request.Headers["Accept"];
            if (acceptHeader.Count > 0)
            {
                upstreamRequest.Headers.TryAddWithoutValidation("Accept", acceptHeader.ToArray());
            }

            using var upstreamResponse = await _httpClientFactory
                .CreateClient(ToolHttpClientNames.Hosting)
                .SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)upstreamResponse.StatusCode is >= 300 and < 400)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            Response.StatusCode = (int)upstreamResponse.StatusCode;
            if (upstreamResponse.Content.Headers.ContentType is not null)
            {
                Response.ContentType = upstreamResponse.Content.Headers.ContentType.ToString();
            }
            if (upstreamResponse.Content.Headers.ContentLength.HasValue)
            {
                Response.ContentLength = upstreamResponse.Content.Headers.ContentLength.Value;
            }
            if (upstreamResponse.Content.Headers.ContentEncoding.Count > 0)
            {
                Response.Headers.ContentEncoding = upstreamResponse.Content.Headers.ContentEncoding.ToArray();
            }
            if (upstreamResponse.Headers.Vary.Count > 0)
            {
                Response.Headers.Vary = upstreamResponse.Headers.Vary.ToArray();
            }
            if (upstreamResponse.Headers.CacheControl is not null)
            {
                Response.Headers["Cache-Control"] = upstreamResponse.Headers.CacheControl.ToString();
            }
            if (upstreamResponse.Headers.ETag is not null)
            {
                Response.Headers["ETag"] = upstreamResponse.Headers.ETag.ToString();
            }

            if (!HttpMethods.IsHead(Request.Method))
            {
                await upstreamResponse.Content.CopyToAsync(Response.Body, cancellationToken);
            }

            return new EmptyResult();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
