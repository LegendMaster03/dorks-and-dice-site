using System.Net;
using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public interface IToolUpstreamPolicy
{
    bool IsAllowed(string? upstreamBaseUrl, out string? reason);
    bool TryBuild(
        ToolRegistration tool,
        string path,
        QueryString queryString,
        out Uri? uri,
        out string? reason);
}

public sealed class ToolUpstreamPolicy : IToolUpstreamPolicy
{
    private readonly HashSet<string> _allowedHosts;

    public ToolUpstreamPolicy(IConfiguration configuration)
    {
        _allowedHosts = configuration
            .GetSection("ToolHosting:AllowedUpstreamHosts")
            .GetChildren()
            .Select(entry => entry.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string? upstreamBaseUrl, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(upstreamBaseUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(upstreamBaseUrl, UriKind.Absolute, out var upstream)
            || (upstream.Scheme != Uri.UriSchemeHttp && upstream.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(upstream.UserInfo))
        {
            reason = "Upstream base URL must be an absolute HTTP or HTTPS URL without embedded credentials.";
            return false;
        }

        if (!string.IsNullOrEmpty(upstream.Fragment) || !string.IsNullOrEmpty(upstream.Query))
        {
            reason = "Upstream base URL can not contain a query string or fragment.";
            return false;
        }

        var host = upstream.Host;
        if (_allowedHosts.Contains(host))
        {
            return true;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var address))
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            reason = $"Literal upstream IP '{host}' is not allowed unless it is listed in ToolHosting:AllowedUpstreamHosts.";
            return false;
        }

        // Docker Compose and similar internal service discovery normally use a single-label DNS name.
        if (!host.Contains('.', StringComparison.Ordinal))
        {
            return true;
        }

        reason = $"Upstream host '{host}' must be an internal single-label service name, localhost, or explicitly listed in ToolHosting:AllowedUpstreamHosts.";
        return false;
    }

    public bool TryBuild(
        ToolRegistration tool,
        string path,
        QueryString queryString,
        out Uri? uri,
        out string? reason)
    {
        uri = null;
        if (!IsAllowed(tool.UpstreamBaseUrl, out reason))
        {
            return false;
        }

        if (!ToolUpstreamUri.TryBuild(tool, path, queryString, out uri) || uri is null)
        {
            reason = "The upstream URL or requested path is invalid.";
            return false;
        }

        return true;
    }
}
