using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Framework.Plugins;

/// <summary>
/// Describes an installed executable extension. Modes may depend on plugin IDs, but the
/// manifest deliberately contains no deployment secrets or mode-specific runtime state.
/// </summary>
public sealed record SitePluginManifest(
    string Id,
    string DisplayName,
    string Version)
{
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}

/// <summary>
/// Startup contract for an installed in-process plugin. Plugins provide executable
/// capabilities and presentations; authored content and mode composition remain runtime data.
/// </summary>
public interface ISitePlugin
{
    SitePluginManifest Manifest { get; }
    void RegisterServices(IServiceCollection services);
}

public interface ISitePluginCatalog
{
    IReadOnlyList<SitePluginManifest> All { get; }
    bool TryGetById(string pluginId, out SitePluginManifest manifest);
}
