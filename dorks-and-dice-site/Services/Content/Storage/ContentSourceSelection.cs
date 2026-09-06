using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content.Storage;

/// <summary>
/// Resolves content-source composition for a request. Normal hosted modes use deployment source
/// policy. Synthetic Development may override that composition through the trusted developer
/// database controls; without an explicit override, the selected normal preview mode keeps its
/// configured source composition. Framework fallback uses Global sources.
/// </summary>
public static class ContentSourceSelection
{
    public static IReadOnlyList<ContentSourceDefinition> GetSourcesForContext(
        this IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        if (modeContext.SyntheticMode is not null
            && modeContext.IsDevelopmentPreview
            && modeContext.HasContentSourceOverride)
        {
            return registry.GetSourcesByKeys(modeContext.EnabledContentSources);
        }

        if (modeContext.ActiveModeId is { Length: > 0 } modeId)
        {
            return registry.GetDefaultSources(modeId);
        }

        if (modeContext.SyntheticMode is not null)
        {
            return [];
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
