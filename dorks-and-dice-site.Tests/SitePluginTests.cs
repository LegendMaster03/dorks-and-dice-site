using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Plugins.ProfessionalPortfolio;
using dorks_and_dice_site.Services.Content;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class SitePluginTests
{
    [Fact]
    public void InstalledPluginPublishesManifestAndContributions()
    {
        var services = new ServiceCollection();
        services.AddSitePlugins([new ProfessionalPortfolioPlugin()]);

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ISitePluginCatalog>();
        var presentations = provider.GetServices<IContentCollectionPresentation>().ToList();

        Assert.True(catalog.TryGetById("professional-portfolio", out var manifest));
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Contains(presentations, presentation => presentation.Key == "professional-experience");
        Assert.Contains(presentations, presentation => presentation.Key == "professional-projects");
    }

    [Fact]
    public void DuplicatePluginIdsAreRejected()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSitePlugins(
        [
            new TestPlugin("duplicate"),
            new TestPlugin("duplicate")
        ]));

        Assert.Contains("Duplicate site plugin ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPluginDependencyIsRejected()
    {
        var services = new ServiceCollection();
        var plugin = new TestPlugin("dependent", ["not-installed"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSitePlugins([plugin]));

        Assert.Contains("requires missing plugin 'not-installed'", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestPlugin(string id, IReadOnlyList<string>? dependencies = null) : ISitePlugin
    {
        public SitePluginManifest Manifest { get; } = new(
            id,
            id,
            "1.0.0")
        {
            Dependencies = dependencies ?? []
        };

        public void RegisterServices(IServiceCollection services)
        {
        }
    }
}
