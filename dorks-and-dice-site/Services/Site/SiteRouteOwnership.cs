using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public static class SiteRouteOwnership
{
    public static bool IsModeAdaptivePath(PathString path)
    {
        return IsModeAdaptivePath(path.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Evaluates route ownership for a normal hosted site mode using registry metadata.
    /// </summary>
    public static bool IsAllowedInMode(PathString path, SiteModeDefinition mode)
    {
        ArgumentNullException.ThrowIfNull(mode);

        var normalizedPath = NormalizePath(path);
        return IsSharedStandardModePath(normalizedPath)
            || IsModeAssetPath(normalizedPath, mode.AssetFolder)
            || mode.OwnedRoutePrefixes.Any(prefix => IsPathWithinPrefix(normalizedPath, prefix))
            || mode.AdditionalAssetPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates the framework fallback used when no normal hosted mode owns the request.
    /// </summary>
    public static bool IsAllowedInFrameworkFallback(PathString path)
    {
        var normalizedPath = NormalizePath(path);
        return IsFallbackPath(normalizedPath)
            || IsContentMediaPath(normalizedPath)
            || IsSharedStaticAssetPath(normalizedPath)
            || IsFrameworkAssetPath(normalizedPath, FrameworkRuntimeStates.Fallback.AssetFolder)
            || IsSharedSystemPath(normalizedPath);
    }

    /// <summary>
    /// Evaluates the request surface while Trusted Preview is active. The selected normal
    /// mode keeps its normal route ownership, while Trusted Preview contributes only its
    /// own framework assets and shared framework routes.
    /// </summary>
    public static bool IsAllowedInTrustedPreview(PathString path, SiteModeDefinition? activeMode)
    {
        var normalizedPath = NormalizePath(path);
        var selectedModeAllowsPath = activeMode is not null && IsAllowedInMode(path, activeMode);

        return selectedModeAllowsPath
            || IsSharedStandardModePath(normalizedPath)
            || IsFrameworkAssetPath(normalizedPath, FrameworkRuntimeStates.TrustedPreview.AssetFolder);
    }

    public static bool IsAllowedInRequest(
        PathString path,
        SiteModeDefinition? activeMode,
        FrameworkRuntimeStateDefinition? frameworkState)
    {
        if (frameworkState == FrameworkRuntimeStates.TrustedPreview)
        {
            return IsAllowedInTrustedPreview(path, activeMode);
        }

        if (activeMode is not null)
        {
            return IsAllowedInMode(path, activeMode);
        }

        return IsAllowedInFrameworkFallback(path);
    }

    /// <summary>
    /// Compatibility bridge for enum-based callers that have not yet migrated to the
    /// registry. New runtime code should pass SiteModeDefinition instead.
    /// </summary>
    public static bool IsAllowedInMode(PathString path, SiteMode siteMode)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var mode))
        {
            return IsAllowedInMode(path, mode!);
        }

        return siteMode switch
        {
            SiteMode.Development => IsAllowedInTrustedPreview(path, activeMode: null),
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
            || path == "/development-preview"
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

    private static bool IsContentMediaPath(string path)
    {
        // Route ownership only permits the request to reach the controller. ContentAssetService
        // still requires a current revision reference from a page visible in the active mode.
        return path.StartsWith("/content/media/", StringComparison.Ordinal);
    }

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
