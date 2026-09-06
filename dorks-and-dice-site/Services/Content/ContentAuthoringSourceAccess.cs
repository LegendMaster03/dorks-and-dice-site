using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Defines source boundaries for the authoring surfaces. Normal hosted-mode authoring remains
/// restricted to the configured authoring workspace. Synthetic Development mirrors the request's
/// explicit content-source composition so its database-source filter remains authoritative.
/// </summary>
public static class ContentAuthoringSourceAccess
{
    public static IReadOnlyList<ContentSourceDefinition> GetModeEditorSources(
        IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(modeContext);

        if (modeContext.SyntheticMode is not null)
        {
            return registry.GetSourcesForContext(modeContext);
        }

        return [registry.GetSource(registry.AuthoringSourceKey)];
    }

    public static IReadOnlyList<ContentSourceDefinition> GetCentralSources(
        IContentSourceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var authoringKey = registry.AuthoringSourceKey;
        return registry.GetAllSources()
            .OrderBy(source => string.Equals(source.Key, authoringKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ResolveModeEditorSourceKey(
        IContentSourceRegistry registry,
        SiteModeContext modeContext,
        string? requestedSourceKey)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(modeContext);

        var sources = GetModeEditorSources(registry, modeContext);
        if (string.IsNullOrWhiteSpace(requestedSourceKey))
        {
            var authoring = sources.FirstOrDefault(source =>
                string.Equals(source.Key, registry.AuthoringSourceKey, StringComparison.OrdinalIgnoreCase));
            return authoring?.Key
                ?? sources.FirstOrDefault()?.Key
                ?? throw new InvalidOperationException(
                    "No content database is selected for the editor in this mode.");
        }

        var requested = requestedSourceKey.Trim();
        var source = sources.SingleOrDefault(candidate =>
            string.Equals(candidate.Key, requested, StringComparison.OrdinalIgnoreCase));
        return source?.Key
            ?? throw new InvalidOperationException(
                $"Content source '{requestedSourceKey}' is not available to the mode editor.");
    }

    public static string ResolveCentralSourceKey(
        IContentSourceRegistry registry,
        string? requestedSourceKey)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var requested = string.IsNullOrWhiteSpace(requestedSourceKey)
            ? registry.AuthoringSourceKey
            : requestedSourceKey.Trim();
        var source = GetCentralSources(registry)
            .SingleOrDefault(candidate =>
                string.Equals(candidate.Key, requested, StringComparison.OrdinalIgnoreCase));
        return source?.Key
            ?? throw new InvalidOperationException(
                $"Content source '{requested}' is not available to central authoring.");
    }
}
