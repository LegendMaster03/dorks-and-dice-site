using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Tools;

public sealed class ToolProxyRequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ToolProxyOptions _options;

    public ToolProxyRequestBodyLimitMiddleware(
        RequestDelegate next,
        IOptions<ToolProxyOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsToolHostUpstreamRequest(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var requestSizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
        {
            requestSizeFeature.MaxRequestBodySize = _options.MaxRequestBodyBytes;
        }

        if (context.Request.ContentLength is long contentLength
            && contentLength > _options.MaxRequestBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                && !context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        }
    }

    public static bool IsToolHostUpstreamRequest(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            && string.Equals(segments[0], "tool-host", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(segments[1])
            && string.Equals(segments[2], "api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[3], "upstream", StringComparison.OrdinalIgnoreCase);
    }
}
