using System.Net;
using System.Text.Json;

namespace dorks_and_dice_site.Services.Site;

public static class DevelopmentAccessEvaluator
{
    public const string AppCapabilitiesHeader = "Tailscale-App-Capabilities";
    public const string DevelopmentCapability = "dorks-and-dice.com/cap/dev-mode";
    public const int TrustedTailscaleIngressPort = 8082;

    public static bool IsAuthorized(HttpContext context, SiteModeOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var host = NormalizeHost(context.Request.Host.Host);
        if (options.DevelopmentHosts.Contains(host) && IsLoopback(context.Connection.RemoteIpAddress))
        {
            return true;
        }

        if (context.Connection.LocalPort != TrustedTailscaleIngressPort)
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue(AppCapabilitiesHeader, out var headerValues))
        {
            return false;
        }

        foreach (var headerValue in headerValues)
        {
            if (ContainsDevelopmentCapability(headerValue))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ContainsDevelopmentCapability(string? serializedCapabilities)
    {
        if (string.IsNullOrWhiteSpace(serializedCapabilities) || serializedCapabilities.Length > 16_384)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedCapabilities, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(DevelopmentCapability, out var capabilityValues)
                || capabilityValues.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return capabilityValues.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsLoopback(IPAddress? address) => address is not null && IPAddress.IsLoopback(address);

    private static string NormalizeHost(string host)
    {
        var normalizedHost = host.ToLowerInvariant();
        return normalizedHost.StartsWith("www.", StringComparison.Ordinal)
            ? normalizedHost[4..]
            : normalizedHost;
    }
}
