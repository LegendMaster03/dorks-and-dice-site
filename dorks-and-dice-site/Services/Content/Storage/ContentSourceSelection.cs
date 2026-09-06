using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content.Storage;

/// <summary>
/// Resolves the content-source composition for a request. Normal hosted modes use deployment
/// source policy, framework fallback uses Global, and synthetic Development uses only the
/// explicit database selection supplied by its trusted developer controls.
/// </summary>
public static class ContentSourceSelection
{
    public static IReadOnlyList<ContentSourceDefinition> GetSourcesForContext(
        this IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        if (modeContext.SyntheticMode is not null)
        {
            return modeContext.HasContentSourceOverride
                ? registry.GetSourcesByKeys(modeContext.EnabledContentSources)
                : [];
        }

        if (modeContext.ActiveModeId is { Length: > 0 } modeId)
        {
            return registry.GetDefaultSources(modeId);
        }

        if (modeContext.IsFrameworkFallback)
        {
            return registry.GetGlobalSources();
        }

        // Temporary compatibility bridge for callers that still construct SiteModeContext
        // through its legacy enum projection. Remove after those callers are migrated.
        return registry.GetDefaultSources(modeContext.SiteMode);
    }
}
