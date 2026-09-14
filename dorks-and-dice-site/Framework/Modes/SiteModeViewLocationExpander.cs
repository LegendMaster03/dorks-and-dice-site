using Microsoft.AspNetCore.Mvc.Razor;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Gives an active normal mode first chance to supply complete MVC views without making
/// controllers or the shared Razor engine branch on named modes.
/// </summary>
public sealed class SiteModeViewLocationExpander : IViewLocationExpander
{
    private const string ViewFolderKey = "site-mode-view-folder";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Values[ViewFolderKey] =
            context.ActionContext.HttpContext.GetSiteModeContext().ActiveMode?.ViewFolder
            ?? string.Empty;
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(viewLocations);

        if (!context.Values.TryGetValue(ViewFolderKey, out var viewFolder)
            || string.IsNullOrWhiteSpace(viewFolder))
        {
            return viewLocations;
        }

        return GetModeLocations(viewFolder).Concat(viewLocations);
    }

    private static IEnumerable<string> GetModeLocations(string viewFolder)
    {
        yield return $"/Views/SiteModes/{viewFolder}/{{1}}/{{0}}.cshtml";
        yield return $"/Views/SiteModes/{viewFolder}/Shared/{{0}}.cshtml";
    }
}
