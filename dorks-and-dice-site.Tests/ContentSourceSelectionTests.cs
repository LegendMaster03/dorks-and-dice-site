using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSourceSelectionTests
{
    [Fact]
    public void SyntheticNormalModeUsesItsStableIdComposition()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            ActiveMode = new SiteModeDefinition(
                Id: "test-mode",
                DisplayName: "Test Mode",
                LegacyMode: null,
                ViewFolder: "TestMode",
                AssetFolder: "test-mode")
        };

        Assert.Equal(
            ["Global", "ModeOnly"],
            registry.GetSourcesForContext(context).Select(source => source.Key));
    }

    [Fact]
    public void SyntheticDevelopmentWithPreviewTargetStillRequiresExplicitDatabaseSelection()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            ActiveMode = new SiteModeDefinition(
                Id: "test-mode",
                DisplayName: "Test Mode",
                LegacyMode: null,
                ViewFolder: "TestMode",
                AssetFolder: "test-mode"),
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true
        };

        Assert.Empty(registry.GetSourcesForContext(context));
    }

    [Fact]
    public void SyntheticDevelopmentWithoutPreviewTargetHasNoImplicitSources()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true
        };

        Assert.Empty(registry.GetSourcesForContext(context));
    }

    [Fact]
    public void FrameworkFallbackUsesGlobalSources()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.Fallback
        };

        Assert.Equal(
            ["Global"],
            registry.GetSourcesForContext(context).Select(source => source.Key));
    }

    [Fact]
    public void SyntheticDevelopmentExplicitDatabaseSelectionWinsOverPreviewTarget()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            ActiveMode = new SiteModeDefinition(
                Id: "test-mode",
                DisplayName: "Test Mode",
                LegacyMode: null,
                ViewFolder: "TestMode",
                AssetFolder: "test-mode"),
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(["Override"], StringComparer.OrdinalIgnoreCase)
        };

        Assert.Equal(
            ["Override"],
            registry.GetSourcesForContext(context).Select(source => source.Key));
    }

    [Fact]
    public void SyntheticDevelopmentExplicitNoneRemainsEmpty()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            ActiveMode = new SiteModeDefinition(
                Id: "test-mode",
                DisplayName: "Test Mode",
                LegacyMode: null,
                ViewFolder: "TestMode",
                AssetFolder: "test-mode"),
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        Assert.Empty(registry.GetSourcesForContext(context));
    }

    private static ContentSourceRegistry CreateRegistry()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GlobalDb"] = "Data Source=global.db",
            ["ConnectionStrings:ModeDb"] = "Data Source=mode.db",
            ["ConnectionStrings:OverrideDb"] = "Data Source=override.db",
            ["ContentStorage:AuthoringSource"] = "Global",
            ["ContentStorage:Sources:Global:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Global:ConnectionString"] = "GlobalDb",
            ["ContentStorage:Sources:ModeOnly:Provider"] = "Sqlite",
            ["ContentStorage:Sources:ModeOnly:ConnectionString"] = "ModeDb",
            ["ContentStorage:Sources:Override:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Override:ConnectionString"] = "OverrideDb",
            ["ContentStorage:GlobalSources:0"] = "Global",
            ["ContentStorage:Modes:test-mode:Add:0"] = "ModeOnly"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new ContentSourceRegistry(configuration, Path.GetTempPath());
    }
}
