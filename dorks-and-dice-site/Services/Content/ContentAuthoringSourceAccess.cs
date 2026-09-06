using dorks_and_dice_site.Services.Content.Storage;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Defines source boundaries for the two authoring surfaces. Normal mode authoring is intentionally
/// restricted to the configured authoring workspace. Cross-source inspection belongs to the trusted
/// Development authoring surface.
/// </summary>
public static class ContentAuthoringSourceAccess
{
    public static IReadOnlyList<ContentSourceDefinition> GetModeEditorSources(
        IContentSourceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
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
        string? requestedSourceKey)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var authoring = registry.GetSource(registry.AuthoringSourceKey);
        if (string.IsNullOrWhiteSpace(requestedSourceKey)
            || string.Equals(requestedSourceKey.Trim(), authoring.Key, StringComparison.OrdinalIgnoreCase))
        {
            return authoring.Key;
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
