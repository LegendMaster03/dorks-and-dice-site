using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Framework.Plugins;

public static class SitePluginRegistrationExtensions
{
    public static IServiceCollection AddSitePlugins(
        this IServiceCollection services,
        IEnumerable<ISitePlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(plugins);

        var installed = plugins.ToList();
        var manifests = Validate(installed);

        foreach (var plugin in installed)
        {
            plugin.RegisterServices(services);
        }

        services.AddSingleton<ISitePluginCatalog>(new SitePluginCatalog(manifests));
        return services;
    }

    private static IReadOnlyList<SitePluginManifest> Validate(IReadOnlyList<ISitePlugin> plugins)
    {
        var manifests = new List<SitePluginManifest>(plugins.Count);
        var byId = new Dictionary<string, SitePluginManifest>(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in plugins)
        {
            var manifest = plugin.Manifest
                ?? throw new InvalidOperationException("An installed site plugin returned no manifest.");

            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new InvalidOperationException("Site plugin IDs can not be blank.");
            }

            if (string.IsNullOrWhiteSpace(manifest.DisplayName))
            {
                throw new InvalidOperationException($"Site plugin '{manifest.Id}' has no display name.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                throw new InvalidOperationException($"Site plugin '{manifest.Id}' has no version.");
            }

            if (!byId.TryAdd(manifest.Id, manifest))
            {
                throw new InvalidOperationException($"Duplicate site plugin ID '{manifest.Id}'.");
            }

            manifests.Add(manifest);
        }

        foreach (var manifest in manifests)
        {
            foreach (var dependency in manifest.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    throw new InvalidOperationException(
                        $"Site plugin '{manifest.Id}' contains a blank dependency ID.");
                }

                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidOperationException(
                        $"Site plugin '{manifest.Id}' requires missing plugin '{dependency}'.");
                }
            }
        }

        return manifests;
    }

    private sealed class SitePluginCatalog : ISitePluginCatalog
    {
        private readonly IReadOnlyDictionary<string, SitePluginManifest> _byId;

        public SitePluginCatalog(IReadOnlyList<SitePluginManifest> manifests)
        {
            All = manifests.ToList().AsReadOnly();
            _byId = manifests.ToDictionary(manifest => manifest.Id, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<SitePluginManifest> All { get; }

        public bool TryGetById(string pluginId, out SitePluginManifest manifest) =>
            _byId.TryGetValue(pluginId, out manifest!);
    }
}
