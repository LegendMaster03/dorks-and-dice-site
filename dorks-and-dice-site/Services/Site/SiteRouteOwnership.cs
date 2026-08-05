using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public static class SiteRouteOwnership
{
    public static bool IsModeAdaptivePath(PathString path)
    {
        return IsModeAdaptivePath(path.ToString().ToLowerInvariant());
    }

    public static bool HasExplicitDevelopmentHandling(PathString path)
    {
        var normalizedPath = path.ToString().ToLowerInvariant();
        return normalizedPath == "/articles" || normalizedPath.StartsWith("/articles/");
    }

    public static SiteMode? GetSingleOwningMode(PathString path)
    {
        var normalizedPath = path.ToString().ToLowerInvariant();
        if (IsProfessionalOwnedPath(normalizedPath))
        {
            return SiteMode.Professional;
        }

        return null;
    }

    public static bool IsAllowedInMode(PathString path, SiteMode siteMode)
    {
        var normalizedPath = path.ToString().ToLowerInvariant();
        var isSharedPath = IsModeAdaptivePath(normalizedPath)
            || IsStaticAssetPath(normalizedPath)
            || IsSharedSystemPath(normalizedPath);

        return siteMode switch
        {
            SiteMode.Development => true,
            SiteMode.Professional => isSharedPath || IsProfessionalOwnedPath(normalizedPath),
            SiteMode.DorksAndDice => isSharedPath,
            SiteMode.Unassigned => IsUnassignedModePath(normalizedPath)
                || IsStaticAssetPath(normalizedPath)
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
            || path == "/home/notfoundpage"
            || path == "/home/error"
            || path == "/home/routeresolutionissue";
    }

    private static bool IsUnassignedModePath(string path)
    {
        return path == "/";
    }

    public static bool IsStaticAssetPath(PathString path)
    {
        return IsStaticAssetPath(path.ToString().ToLowerInvariant());
    }

    private static bool IsStaticAssetPath(string path)
    {
        return path.StartsWith("/css/")
            || path.StartsWith("/js/")
            || path.StartsWith("/lib/")
            || path.StartsWith("/images/")
            || path.StartsWith("/files/")
            || path.StartsWith("/favicon")
            || path.StartsWith("/robots.txt");
    }
}
