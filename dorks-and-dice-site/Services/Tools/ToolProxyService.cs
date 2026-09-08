namespace dorks_and_dice_site.Services.Tools;

public interface IToolProxyService
{
    Task ProxyAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        CancellationToken cancellationToken = default);

    Task ProxyAuthenticatedAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        string authenticationTicket,
        string introspectionPath,
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

    public Task ProxyAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        CancellationToken cancellationToken = default) =>
        ProxyCoreAsync(context, tool, path, trustedRequestHeaders: null, cancellationToken);

    public Task ProxyAuthenticatedAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        string authenticationTicket,
        string introspectionPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationTicket);
        ArgumentException.ThrowIfNullOrWhiteSpace(introspectionPath);

        IReadOnlyDictionary<string, string> trustedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ToolAuthenticationHeaders.Ticket] = authenticationTicket,
            [ToolAuthenticationHeaders.IntrospectionPath] = introspectionPath
        };

        return ProxyCoreAsync(context, tool, path, trustedHeaders, cancellationToken);
    }

    private async Task ProxyCoreAsync(
        HttpContext context,
        Models.Tools.ToolRegistration tool,
        string path,
        IReadOnlyDictionary<string, string>? trustedRequestHeaders,
        CancellationToken cancellationToken)
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
            if (trustedRequestHeaders is not null)
            {
                foreach (var header in trustedRequestHeaders)
                {
                    upstreamRequest.Headers.Remove(header.Key);
                    upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var upstreamResponse = await _httpClientFactory
                .CreateClient(ToolHttpClientNames.Proxy)
                .SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)upstreamResponse.StatusCode is >= 300 and < 400
                && upstreamResponse.StatusCode != System.Net.HttpStatusCode.NotModified)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyResponseHeaders(context.Response, upstreamResponse);
            if (trustedRequestHeaders is not null)
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            if (!HttpMethods.IsHead(context.Request.Method)
                && upstreamResponse.StatusCode != System.Net.HttpStatusCode.NotModified)
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
        var connectionHeaders = ConnectionHeaderNames(context.Request.Headers.Connection.Select(value => value ?? string.Empty));
        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key)
                || IsBlockedRequestHeader(header.Key)
                || connectionHeaders.Contains(header.Key))
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

    private static bool IsBlockedRequestHeader(string headerName) =>
        BlockedRequestHeaders.Contains(headerName)
        || headerName.StartsWith(ToolAuthenticationHeaders.ReservedPrefix, StringComparison.OrdinalIgnoreCase);

    private static void CopyResponseHeaders(HttpResponse response, HttpResponseMessage upstreamResponse)
    {
        var connectionHeaders = ConnectionHeaderNames(upstreamResponse.Headers.Connection);
        foreach (var header in upstreamResponse.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || BlockedResponseHeaders.Contains(header.Key)
                || connectionHeaders.Contains(header.Key))
            {
                continue;
            }

            response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || BlockedResponseHeaders.Contains(header.Key)
                || connectionHeaders.Contains(header.Key))
            {
                continue;
            }

            response.Headers[header.Key] = header.Value.ToArray();
        }

        response.Headers.Remove("transfer-encoding");
    }

    private static HashSet<string> ConnectionHeaderNames(IEnumerable<string> values) =>
        values.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
