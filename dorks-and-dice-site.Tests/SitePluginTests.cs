using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Plugins.DiscordWidget;
using dorks_and_dice_site.Plugins.ProfessionalPortfolio;
using dorks_and_dice_site.Services.Content;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class SitePluginTests
{
    [Fact]
    public void InstalledPluginsPublishManifestsAndContributions()
    {
        var services = new ServiceCollection();
        services.AddSitePlugins(
        [
            new ProfessionalPortfolioPlugin(),
            new DiscordWidgetPlugin()
        ]);

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ISitePluginCatalog>();
        var presentations = provider.GetServices<IContentCollectionPresentation>().ToList();
        var pageComponents = provider.GetServices<IContentPageComponentDefinition>().ToList();

        Assert.True(catalog.TryGetById("professional-portfolio", out var portfolioManifest));
        Assert.Equal("1.0.0", portfolioManifest.Version);
        Assert.True(catalog.TryGetById("discord-widget", out var discordManifest));
        Assert.Equal("1.1.0", discordManifest.Version);
        Assert.Contains(presentations, presentation => presentation.Key == "professional-experience");
        Assert.Contains(presentations, presentation => presentation.Key == "professional-projects");
        Assert.Contains(pageComponents, component => component.Name == "discord-widget");
    }

    [Fact]
    public void DiscordWidgetRequiresServerIdAndRestrictsTheme()
    {
        var services = new ServiceCollection();
        services.AddSitePlugins([new DiscordWidgetPlugin()]);

        using var provider = services.BuildServiceProvider();
        var component = Assert.Single(provider.GetServices<IContentPageComponentDefinition>());

        var missingId = Assert.Throws<InvalidOperationException>(() =>
            component.Validate(new Dictionary<string, string>()));
        Assert.Contains("server-id", missingId.Message, StringComparison.OrdinalIgnoreCase);

        component.Validate(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["server-id"] = "1281714470799806545",
            ["theme"] = "dark",
            ["title"] = "Dorks & Dice Discord Server"
        });

        var invalidTheme = Assert.Throws<InvalidOperationException>(() =>
            component.Validate(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["server-id"] = "1281714470799806545",
                ["theme"] = "neon"
            }));
        Assert.Contains("theme", invalidTheme.Message, StringComparison.OrdinalIgnoreCase);
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
