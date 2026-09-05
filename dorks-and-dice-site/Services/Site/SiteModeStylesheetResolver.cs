using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeStylesheetResolver : ISiteModeStylesheetResolver
{
    // Trusted Preview still uses the legacy development asset location during migration.
    private const string TrustedPreviewStylesheetPath = "~/site-modes/development/css/site.css";

    public IReadOnlyList<string> GetStylesheetPaths(SiteModeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var paths = new List<string>(2);
        if (context.ActiveMode is not null)
        {
            paths.Add(BuildModeStylesheetPath(context.ActiveMode));
        }

        if (context.IsTrustedPreview)
        {
            paths.Add(TrustedPreviewStylesheetPath);
        }

        return paths;
    }

    public IReadOnlyList<string> GetStylesheetPaths(SiteMode siteMode, bool includeDevelopmentTools)
    {
        SiteModeDefinition? activeMode = null;
        FrameworkRuntimeStateDefinition? frameworkState = null;

        if (BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var definition))
        {
            activeMode = definition;
        }
        else if (FrameworkRuntimeStates.TryGetByLegacyMode(siteMode, out var state))
        {
            frameworkState = state;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(siteMode), siteMode, "Unknown site mode.");
        }

        if (includeDevelopmentTools)
        {
            frameworkState = FrameworkRuntimeStates.TrustedPreview;
        }

        return GetStylesheetPaths(new SiteModeContext
        {
            ActiveMode = activeMode,
            FrameworkState = frameworkState
        });
    }

    private static string BuildModeStylesheetPath(SiteModeDefinition mode) =>
        $"~/site-modes/{mode.AssetFolder}/css/site.css";
}
