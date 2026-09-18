using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public static class SiteRouteOwnership
{
    public static bool IsModeAdaptivePath(PathString path)
    {
        return IsModeAdaptivePath(path.ToString().ToLowerInvariant());
    }

    public static bool IsAllowedInMode(PathString path, SiteModeDefinition mode)
    {
        ArgumentNullException.ThrowIfNull(mode);

        var normalizedPath = NormalizePath(path);
        return IsSharedStandardModePath(normalizedPath)
            || IsModeAssetPath(normalizedPath, mode.AssetFolder)
            || mode.OwnedRoutePrefixes.Any(prefix => IsPathWithinPrefix(normalizedPath, prefix))
            || mode.AdditionalAssetPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsAllowedInFrameworkFallback(PathString path)
    {
        var normalizedPath = NormalizePath(path);
        return IsFallbackPath(normalizedPath)
            || IsContentMediaPath(normalizedPath)
            || IsSharedStaticAssetPath(normalizedPath)
            || IsPluginPath(normalizedPath)
            || IsFrameworkAssetPath(normalizedPath, FrameworkRuntimeStates.Fallback.AssetFolder)
            || IsSharedSystemPath(normalizedPath);
    }

    public static bool IsAllowedInSyntheticMode(
        PathString path,
        SyntheticSiteModeDefinition syntheticMode,
        SiteModeDefinition? previewMode)
    {
        ArgumentNullException.ThrowIfNull(syntheticMode);

        var normalizedPath = NormalizePath(path);
        var selectedModeAllowsPath = previewMode is not null && IsAllowedInMode(path, previewMode);

        return selectedModeAllowsPath
            || IsSharedStandardModePath(normalizedPath)
            || IsFrameworkAssetPath(normalizedPath, syntheticMode.AssetFolder);
    }

    public static bool IsAllowedInTrustedPreview(PathString path, SiteModeDefinition? activeMode) =>
        IsAllowedInSyntheticMode(path, SyntheticSiteModes.Development, activeMode);

    public static bool IsAllowedInRequest(
        PathString path,
        SiteModeDefinition? activeMode,
        FrameworkRuntimeStateDefinition? frameworkState)
    {
        if (frameworkState is SyntheticSiteModeDefinition syntheticMode)
        {
            return IsAllowedInSyntheticMode(path, syntheticMode, activeMode);
        }

        if (activeMode is not null)
        {
            return IsAllowedInMode(path, activeMode);
        }

        return IsAllowedInFrameworkFallback(path);
    }

    public static bool IsAllowedInMode(PathString path, SiteMode siteMode)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var mode))
        {
            return IsAllowedInMode(path, mode!);
        }

        return siteMode switch
        {
            SiteMode.Development => IsAllowedInSyntheticMode(
                path,
                SyntheticSiteModes.Development,
                previewMode: null),
            SiteMode.Unassigned => IsAllowedInFrameworkFallback(path),
            _ => false
        };
    }

    private static string NormalizePath(PathString path) =>
        path.ToString().ToLowerInvariant();

    private static bool IsSharedStandardModePath(string path)
    {
        return IsModeAdaptivePath(path)
            || IsContentMediaPath(path)
            || IsSharedStaticAssetPath(path)
            || IsPluginPath(path)
            || IsFrameworkAssetPath(path, FrameworkRuntimeStates.Fallback.AssetFolder)
            || IsSharedSystemPath(path);
    }

    private static bool IsModeAdaptivePath(string path)
    {
        return path == "/"
            || path == "/articles"
            || path.StartsWith("/articles/")
            || path == "/tools"
            || path.StartsWith("/tools/", StringComparison.Ordinal)
            || path == "/tool-modules"
            || path.StartsWith("/tool-modules/", StringComparison.Ordinal)
            || path == "/tool-host"
            || path.StartsWith("/tool-host/", StringComparison.Ordinal);
    }

    private static bool IsSharedSystemPath(string path)
    {
        return path == "/health"
            || path == "/robots.txt"
            || path == "/sitemap.xml"
            || path == "/site.txt"
            || path == "/llms.txt"
            || path == "/development-preview"
            || IsToolHostIntrospectionPath(path)
            || IsToolHostDelegatedUpstreamPath(path)
            || path == "/operator"
            || path.StartsWith("/operator/", StringComparison.Ordinal)
            || path == "/account"
            || path.StartsWith("/account/", StringComparison.Ordinal)
            || path == "/editor"
            || path.StartsWith("/editor/", StringComparison.Ordinal)
            || path == "/admin"
            || path.StartsWith("/admin/", StringComparison.Ordinal)
            || path == "/development"
            || path.StartsWith("/development/", StringComparison.Ordinal)
            || path == "/home/notfoundpage"
            || path == "/home/error"
            || path == "/home/routeresolutionissue";
    }

    private static bool IsToolHostIntrospectionPath(string path)
    {
        const string prefix = "/tool-host/";
        const string suffix = "/api/introspect";

        if (!path.StartsWith(prefix, StringComparison.Ordinal)
            || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var slugLength = path.Length - prefix.Length - suffix.Length;
        if (slugLength <= 0)
        {
            return false;
        }

        var slug = path.AsSpan(prefix.Length, slugLength);
        return !slug.Contains('/');
    }

    private static bool IsToolHostDelegatedUpstreamPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 6
            && string.Equals(segments[0], "tool-host", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(segments[1])
            && string.Equals(segments[2], "api", StringComparison.Ordinal)
            && string.Equals(segments[3], "delegate", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(segments[4])
            && string.Equals(segments[5], "upstream", StringComparison.Ordinal);
    }

    private static bool IsContentMediaPath(string path)
    {
        return path.StartsWith("/content/media/", StringComparison.Ordinal);
    }

    private static bool IsPluginPath(string path) =>
        path.StartsWith("/plugins/", StringComparison.Ordinal);

    private static bool IsFallbackPath(string path) => path == "/";

    private static bool IsSharedStaticAssetPath(string path)
    {
        return path.StartsWith("/css/")
            || path.StartsWith("/js/")
            || path.StartsWith("/lib/")
            || path.StartsWith("/shared/")
            || path == "/dorks-and-dice-site.styles.css"
            || (path.StartsWith("/dorks-and-dice-site.", StringComparison.Ordinal)
                && path.EndsWith(".styles.css", StringComparison.Ordinal))
            || path.StartsWith("/favicon");
    }

    private static bool IsModeAssetPath(string path, string assetFolder) =>
        !string.IsNullOrWhiteSpace(assetFolder)
        && path.StartsWith($"/site-modes/{assetFolder.Trim('/')}/", StringComparison.OrdinalIgnoreCase);

    private static bool IsFrameworkAssetPath(string path, string assetFolder) =>
        IsModeAssetPath(path, assetFolder);

    private static bool IsPathWithinPrefix(string path, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        var normalizedPrefix = prefix.Trim().ToLowerInvariant();
        if (!normalizedPrefix.StartsWith('/'))
        {
            normalizedPrefix = $"/{normalizedPrefix}";
        }

        normalizedPrefix = normalizedPrefix.TrimEnd('/');
        return path == normalizedPrefix
            || path.StartsWith($"{normalizedPrefix}/", StringComparison.Ordinal);
    }
}
