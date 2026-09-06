using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Defines source boundaries for the authoring surfaces. Normal hosted-mode authoring remains
/// restricted to the configured authoring workspace. In Trusted Preview, the mode editor mirrors
/// the request's content-source composition so the database-source filter controls which databases
/// contribute articles to the editor index.
/// </summary>
public static class ContentAuthoringSourceAccess
{
    public static IReadOnlyList<ContentSourceDefinition> GetModeEditorSources(
        IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(modeContext);

        if (modeContext.IsTrustedPreview)
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

        var authoring = registry.GetSource(registry.AuthoringSourceKey);
        if (string.IsNullOrWhiteSpace(requestedSourceKey)
            || string.Equals(requestedSourceKey.Trim(), authoring.Key, StringComparison.OrdinalIgnoreCase))
        {
            return authoring.Key;
        }

        if (modeContext.IsTrustedPreview)
        {
            var requested = requestedSourceKey.Trim();
            var source = GetModeEditorSources(registry, modeContext)
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.Key, requested, StringComparison.OrdinalIgnoreCase));
            if (source is not null)
            {
                return source.Key;
            }
        }

        throw new InvalidOperationException(
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
