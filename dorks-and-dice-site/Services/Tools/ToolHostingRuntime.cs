using System.Diagnostics;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolHttpClientNames
{
    public const string Hosting = "tool-hosting";
    public const string Proxy = "tool-proxy";
}

public static class ToolVisibility
{
    public static bool IsVisibleInMode(ToolRegistration tool, SiteMode siteMode)
    {
        if (!BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var definition))
        {
            return false;
        }

        return IsVisibleInMode(tool, definition!.Id);
    }

    public static bool IsVisibleInMode(ToolRegistration tool, string? modeId)
    {
        if (string.IsNullOrWhiteSpace(modeId))
        {
            return false;
        }

        // Registrations created before mode selection existed were Dorks & Dice-only.
        // Keep that compatibility policy explicit and isolated until those registrations
        // are migrated to an explicit mode list.
        if (tool.Modes is null || tool.Modes.Count == 0)
        {
            return string.Equals(modeId, SiteModeValues.DorksAndDiceModeValue, StringComparison.Ordinal);
        }

        return tool.Modes.Contains(modeId, StringComparer.Ordinal);
    }
}

public static class ToolUpstreamUri
{
    public static bool TryBuild(
        ToolRegistration tool,
        string path,
        QueryString queryString,
        out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl)
            || !Uri.TryCreate(tool.UpstreamBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
        if (ContainsTraversal(normalizedPath))
        {
            return false;
        }

        var target = $"{baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}{normalizedPath}";
        if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri))
        {
            return false;
        }

        if (queryString.HasValue)
        {
            var builder = new UriBuilder(targetUri)
            {
                Query = queryString.Value![1..]
            };
            targetUri = builder.Uri;
        }

        uri = targetUri;
        return true;
    }

    private static bool ContainsTraversal(string path)
    {
        // Validate each decoding layer: upstream servers differ in when they decode
        // path escapes and whether backslashes are treated as separators.
        for (var depth = 0; depth < 8; depth++)
        {
            if (path.Contains('\\')
                || path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                return true;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(path);
            }
            catch (UriFormatException)
            {
                return true;
            }
            if (decoded == path)
            {
                return false;
            }
            path = decoded;
        }

        return true;
    }
}

public interface IToolHealthService
{
    Task<ToolHealthResult> CheckAsync(ToolRegistration tool, CancellationToken cancellationToken = default);
}

public sealed class ToolHealthService : IToolHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IToolUpstreamPolicy _upstreamPolicy;

    public ToolHealthService(
        IHttpClientFactory httpClientFactory,
        IToolUpstreamPolicy upstreamPolicy)
    {
        _httpClientFactory = httpClientFactory;
        _upstreamPolicy = upstreamPolicy;
    }

    public async Task<ToolHealthResult> CheckAsync(
        ToolRegistration tool,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tool.UpstreamBaseUrl)
            || string.IsNullOrWhiteSpace(tool.HealthPath))
        {
            return new ToolHealthResult(
                ToolHealthStatus.NotConfigured,
                "No health endpoint configured.",
                null,
                null);
        }

        if (!_upstreamPolicy.TryBuild(tool, tool.HealthPath, QueryString.Empty, out var healthUri, out _)
            || healthUri is null)
        {
            return new ToolHealthResult(
                ToolHealthStatus.Unhealthy,
                "Health endpoint configuration is invalid or disallowed.",
                null,
                null);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, healthUri);
            using var response = await _httpClientFactory
                .CreateClient(ToolHttpClientNames.Hosting)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();

            return response.IsSuccessStatusCode
                ? new ToolHealthResult(
                    ToolHealthStatus.Healthy,
                    $"HTTP {(int)response.StatusCode}",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds)
                : new ToolHealthResult(
                    ToolHealthStatus.Unhealthy,
                    $"HTTP {(int)response.StatusCode}",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ToolHealthResult(
                ToolHealthStatus.Unhealthy,
                "Health check timed out.",
                null,
                stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            return new ToolHealthResult(
                ToolHealthStatus.Unhealthy,
                exception.Message,
                null,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
