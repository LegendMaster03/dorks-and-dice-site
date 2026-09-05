namespace dorks_and_dice_site.Services.Tools;

public interface IToolProxyService
{
    Task ProxyAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        CancellationToken cancellationToken = default);
}

public sealed class ToolProxyService : IToolProxyService
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

    private static readonly HashSet<string> BlockedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Host",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Forwarded-Prefix",
        "X-Dorks-Tool-Context-Url"
    };

    private static readonly HashSet<string> BlockedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set-Cookie"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IToolUpstreamPolicy _upstreamPolicy;

    public ToolProxyService(
        IHttpClientFactory httpClientFactory,
        IToolUpstreamPolicy upstreamPolicy)
    {
        _httpClientFactory = httpClientFactory;
        _upstreamPolicy = upstreamPolicy;
    }

    public async Task ProxyAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_upstreamPolicy.TryBuild(tool, path, context.Request.QueryString, out var upstreamUri, out _)
            || upstreamUri is null)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        try
        {
            using var upstreamRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), upstreamUri);
            if (HasRequestBody(context.Request))
            {
                upstreamRequest.Content = new StreamContent(context.Request.Body);
            }

            CopyRequestHeaders(context, upstreamRequest, tool.Slug);

            using var upstreamResponse = await _httpClientFactory
                .CreateClient(ToolHttpClientNames.Proxy)
                .SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)upstreamResponse.StatusCode is >= 300 and < 400)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyResponseHeaders(context.Response, upstreamResponse);

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await upstreamResponse.Content.CopyToAsync(context.Response.Body, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        }
        catch (HttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
    }

    private static bool HasRequestBody(HttpRequest request) =>
        request.ContentLength > 0
        || request.Headers.ContainsKey("Transfer-Encoding")
        || HttpMethods.IsPost(request.Method)
        || HttpMethods.IsPut(request.Method)
        || HttpMethods.IsPatch(request.Method);

    private static void CopyRequestHeaders(
        HttpContext context,
        HttpRequestMessage upstreamRequest,
        string toolSlug)
    {
        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || BlockedRequestHeaders.Contains(header.Key))
            {
                continue;
            }

            if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                upstreamRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", $"/tools/{toolSlug}");
        upstreamRequest.Headers.TryAddWithoutValidation("X-Dorks-Tool-Context-Url", $"/tool-host/{toolSlug}/context");
    }

    private static void CopyResponseHeaders(HttpResponse response, HttpResponseMessage upstreamResponse)
    {
        foreach (var header in upstreamResponse.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || BlockedResponseHeaders.Contains(header.Key))
            {
                continue;
            }

            response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || BlockedResponseHeaders.Contains(header.Key))
            {
                continue;
            }

            response.Headers[header.Key] = header.Value.ToArray();
        }

        response.Headers.Remove("transfer-encoding");
    }
}
