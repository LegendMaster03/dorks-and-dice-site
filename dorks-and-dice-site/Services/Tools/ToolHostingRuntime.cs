using System.Diagnostics;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolHttpClientNames
{
    public const string Hosting = "tool-hosting";
}

public static class ToolVisibility
{
    public static bool IsVisibleInMode(ToolRegistration tool, SiteMode siteMode)
    {
        var modeValue = siteMode switch
        {
            SiteMode.DorksAndDice => SiteModeValues.DorksAndDiceModeValue,
            SiteMode.Professional => SiteModeValues.ProfessionalModeValue,
            _ => null
        };

        if (modeValue is null)
        {
            return false;
        }

        // Registrations created before mode selection existed were Dorks & Dice-only.
        if (tool.Modes is null || tool.Modes.Count == 0)
        {
            return string.Equals(modeValue, SiteModeValues.DorksAndDiceModeValue, StringComparison.Ordinal);
        }

        return tool.Modes.Contains(modeValue, StringComparer.Ordinal);
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
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            return true;
        }

        return decoded
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }
}

public interface IToolHealthService
{
    Task<ToolHealthResult> CheckAsync(ToolRegistration tool, CancellationToken cancellationToken = default);
}

public sealed class ToolHealthService : IToolHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ToolHealthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
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

        if (!ToolUpstreamUri.TryBuild(tool, tool.HealthPath, QueryString.Empty, out var healthUri)
            || healthUri is null)
        {
            return new ToolHealthResult(
                ToolHealthStatus.Unhealthy,
                "Health endpoint configuration is invalid.",
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
