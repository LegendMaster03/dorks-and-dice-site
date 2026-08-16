using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public static class SiteRouteOwnership
{
    public static bool IsModeAdaptivePath(PathString path)
    {
        return IsModeAdaptivePath(path.ToString().ToLowerInvariant());
    }

    public static bool IsAllowedInMode(PathString path, SiteMode siteMode)
    {
        var normalizedPath = path.ToString().ToLowerInvariant();
        var isSharedPath = IsModeAdaptivePath(normalizedPath)
            || IsContentMediaPath(normalizedPath)
            || IsSharedStaticAssetPath(normalizedPath)
            || IsUnassignedAssetPath(normalizedPath)
            || IsSharedSystemPath(normalizedPath);

        return siteMode switch
        {
            SiteMode.Development => true,
            SiteMode.Professional => isSharedPath
                || IsProfessionalOwnedPath(normalizedPath)
                || IsProfessionalAssetPath(normalizedPath)
                || IsAssetException(SiteMode.Professional, normalizedPath),
            SiteMode.DorksAndDice => isSharedPath || IsDorksAndDiceAssetPath(normalizedPath),
            SiteMode.Unassigned => IsUnassignedModePath(normalizedPath)
                || IsContentMediaPath(normalizedPath)
                || IsSharedStaticAssetPath(normalizedPath)
                || IsUnassignedAssetPath(normalizedPath)
                || IsSharedSystemPath(normalizedPath),
            _ => false
        };
    }

    private static bool IsModeAdaptivePath(string path)
    {
        return path == "/" || path == "/articles" || path.StartsWith("/articles/");
    }

    private static bool IsProfessionalOwnedPath(string path)
    {
        return path == "/resume" || path.StartsWith("/resume/");
    }

    private static bool IsSharedSystemPath(string path)
    {
        return path == "/health"
            || path == "/robots.txt"
            || path == "/sitemap.xml"
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

    private static bool IsUnassignedModePath(string path)
    {
        return path == "/";
    }

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

    private static bool IsProfessionalAssetPath(string path)
    {
        return path.StartsWith("/site-modes/professional/");
    }

    private static bool IsDorksAndDiceAssetPath(string path)
    {
        return path.StartsWith("/site-modes/dorks-and-dice/");
    }

    private static bool IsAssetException(SiteMode siteMode, string path)
    {
        return siteMode switch
        {
            SiteMode.Professional => path == "/site-modes/dorks-and-dice/images/favicon.svg",
            _ => false
        };
    }

    private static bool IsUnassignedAssetPath(string path)
    {
        return path.StartsWith("/site-modes/unassigned/");
    }
}
