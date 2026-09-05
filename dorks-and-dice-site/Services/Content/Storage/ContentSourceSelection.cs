using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content.Storage;

/// <summary>
/// Resolves the content-source composition for a request without treating framework
/// fallback or Trusted Preview as normal site modes.
/// </summary>
public static class ContentSourceSelection
{
    public static IReadOnlyList<ContentSourceDefinition> GetSourcesForContext(
        this IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        if (modeContext.IsDevelopmentPreview && modeContext.HasContentSourceOverride)
        {
            return registry.GetSourcesByKeys(modeContext.EnabledContentSources);
        }

        if (modeContext.ActiveModeId is { Length: > 0 } modeId)
        {
            return registry.GetDefaultSources(modeId);
        }

        if (modeContext.IsTrustedPreview)
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
