using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Defines which configured content sources the browser authoring surfaces may inspect or edit for
/// the current request. The configured authoring workspace is always retained so editors can keep
/// working locally even when Trusted Preview is currently composed from other sources.
/// </summary>
public static class ContentAuthoringSourceAccess
{
    public static IReadOnlyList<ContentSourceDefinition> GetAccessibleSources(
        IContentSourceRegistry registry,
        SiteModeContext modeContext)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(modeContext);

        var sources = new List<ContentSourceDefinition>
        {
            registry.GetSource(registry.AuthoringSourceKey)
        };

        foreach (var source in registry.GetSourcesForContext(modeContext))
        {
            if (sources.All(existing =>
                    !string.Equals(existing.Key, source.Key, StringComparison.OrdinalIgnoreCase)))
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    public static string ResolveAccessibleSourceKey(
        IContentSourceRegistry registry,
        SiteModeContext modeContext,
        string? requestedSourceKey)
    {
        var requested = string.IsNullOrWhiteSpace(requestedSourceKey)
            ? registry.AuthoringSourceKey
            : requestedSourceKey.Trim();

        var source = GetAccessibleSources(registry, modeContext)
            .SingleOrDefault(candidate =>
                string.Equals(candidate.Key, requested, StringComparison.OrdinalIgnoreCase));

        return source?.Key
            ?? throw new InvalidOperationException(
                $"Content source '{requested}' is not available to the current authoring context.");
    }
}
