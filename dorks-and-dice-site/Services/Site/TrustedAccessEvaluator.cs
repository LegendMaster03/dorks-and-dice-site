using System.Net;
using System.Text.Json;

namespace dorks_and_dice_site.Services.Site;

public static class TrustedAccessEvaluator
{
    private static readonly object OriginalConnectionItemKey = new();

    public const string AppCapabilitiesHeader = "Tailscale-App-Capabilities";
    public const string DevelopmentCapability = "dorks-and-dice.com/cap/dev-mode";
    public const int TrustedTailscaleIngressPort = 8082;

    public static void CaptureOriginalConnection(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[OriginalConnectionItemKey] = new OriginalConnection(context.Connection.RemoteIpAddress);
    }

    public static bool IsAuthorized(HttpContext context, SiteModeOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var host = NormalizeHost(context.Request.Host.Host);
        if (options.DevelopmentHosts.Contains(host) && IsTrustedLocalRequest(context))
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

    private static bool IsTrustedLocalRequest(HttpContext context)
    {
        var remoteAddress = context.Items.TryGetValue(OriginalConnectionItemKey, out var value)
            && value is OriginalConnection originalConnection
                ? originalConnection.RemoteIpAddress
                : context.Connection.RemoteIpAddress;

        if (remoteAddress is not null)
        {
            return IPAddress.IsLoopback(remoteAddress);
        }

        // ASP.NET's in-memory TestServer does not create a TCP connection. Its synthetic
        // requests therefore have no peer address and zero local/remote ports. An actual
        // established network request can not match this shape.
        return context.Connection.LocalPort == 0 && context.Connection.RemotePort == 0;
    }

    private sealed record OriginalConnection(IPAddress? RemoteIpAddress);

    private static string NormalizeHost(string host)
    {
        var normalizedHost = host.ToLowerInvariant();
        return normalizedHost.StartsWith("www.", StringComparison.Ordinal)
            ? normalizedHost[4..]
            : normalizedHost;
    }
}
