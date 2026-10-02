using System.Diagnostics;
using System.Security.Claims;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
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

        return GetEffectiveModeIds(tool).Contains(modeId, StringComparer.Ordinal);
    }

    public static bool IsVisibleToUser(
        ToolRegistration tool,
        string? modeId,
        ClaimsPrincipal principal) =>
        tool.Kind == ToolKind.Application
        && ToolPublicRoute.CanBuild(tool)
        && tool.Enabled
        && IsVisibleInMode(tool, modeId)
        && CanUseReleaseAudience(tool, modeId, principal)
        && (principal.Identity?.IsAuthenticated == true
            || IsPubliclyDiscoverable(tool, modeId));

    /// <summary>
    /// Returns whether an application is intentionally public, anonymously reachable, and
    /// configured well enough to serve its public route in the supplied site mode. Search
    /// discovery surfaces must use this narrower policy instead of user-specific visibility so
    /// Development, Testing, account-required, disabled, unsupported, incomplete, and service
    /// registrations never leak into public indexes or sitemaps.
    /// </summary>
    public static bool IsPubliclyDiscoverable(ToolRegistration tool, string? modeId) =>
        tool.Kind == ToolKind.Application
        && ToolPublicRoute.CanBuild(tool)
        && tool.Enabled
        && tool.ReleaseAudience == ToolReleaseAudience.Public
        && tool.AllowAnonymous
        && IsVisibleInMode(tool, modeId)
        && ToolIntegrationContractPolicy.IsSupported(tool)
        && HasPublicRouteConfiguration(tool);

    public static bool CanUseReleaseAudience(
        ToolRegistration tool,
        string? modeId,
        ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(principal);

        if (tool.Kind != ToolKind.Application || string.IsNullOrWhiteSpace(modeId))
        {
            return false;
        }

        return tool.ReleaseAudience switch
        {
            ToolReleaseAudience.Development =>
                principal.Identity?.IsAuthenticated == true
                && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.Dev),
            ToolReleaseAudience.Testing =>
                principal.Identity?.IsAuthenticated == true
                && AccountRoleHierarchy.PrincipalHasScopedRole(
                    principal,
                    modeId,
                    ScopedAccountRoles.Tester),
            ToolReleaseAudience.Public => true,
            _ => false
        };
    }

    public static IReadOnlyList<string> GetEffectiveModeIds(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // Registrations created before mode selection existed were Dorks & Dice-only.
        // Keep that compatibility policy in one boundary until those persisted registrations
        // have been migrated to an explicit mode list.
        return tool.Modes is { Count: > 0 }
            ? tool.Modes
            : [SiteModeValues.DorksAndDiceModeValue];
    }

    private static bool HasPublicRouteConfiguration(ToolRegistration tool)
    {
        if (!ToolUpstreamUri.TryBuild(tool, "/", QueryString.Empty, out _))
        {
            return false;
        }

        return tool.IntegrationType switch
        {
            ToolIntegrationType.EmbeddedModule =>
                !string.IsNullOrWhiteSpace(tool.FrontendEntryPoint)
                && tool.FrontendEntryPoint.StartsWith("/", StringComparison.Ordinal)
                && ToolUpstreamUri.TryBuild(tool, tool.FrontendEntryPoint, QueryString.Empty, out _),
            ToolIntegrationType.ProxiedApplication => true,
            _ => false
        };
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
