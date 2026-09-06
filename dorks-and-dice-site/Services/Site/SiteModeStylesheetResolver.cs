using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeStylesheetResolver : ISiteModeStylesheetResolver
{
    public IReadOnlyList<string> GetStylesheetPaths(SiteModeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var paths = new List<string>(2);
        if (context.ActiveMode is not null)
        {
            paths.Add(BuildStylesheetPath(context.ActiveMode.AssetFolder));
        }

        if (context.SyntheticMode is not null)
        {
            paths.Add(BuildStylesheetPath(context.SyntheticMode.AssetFolder));
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
            frameworkState = SyntheticSiteModes.Development;
        }

        return GetStylesheetPaths(new SiteModeContext
        {
            ActiveMode = activeMode,
            FrameworkState = frameworkState
        });
    }

    private static string BuildStylesheetPath(string assetFolder) =>
        $"~/site-modes/{assetFolder}/css/site.css";
}
