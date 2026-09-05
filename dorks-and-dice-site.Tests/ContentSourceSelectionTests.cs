using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSourceSelectionTests
{
    [Fact]
    public void SyntheticStandardModeUsesItsStableIdComposition()
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
    public void TrustedPreviewWithTargetUsesTargetComposition()
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
            FrameworkState = FrameworkRuntimeStates.TrustedPreview,
            IsDevelopmentPreview = true
        };

        Assert.Equal(
            ["Global", "ModeOnly"],
            registry.GetSourcesForContext(context).Select(source => source.Key));
    }

    [Fact]
    public void TrustedPreviewWithoutTargetHasNoNormalSiteSources()
    {
        var registry = CreateRegistry();
        var context = new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.TrustedPreview,
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
    public void TrustedPreviewSourceOverrideWinsOverSelectedMode()
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
            FrameworkState = FrameworkRuntimeStates.TrustedPreview,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(["Override"], StringComparer.OrdinalIgnoreCase)
        };

        Assert.Equal(
            ["Override"],
            registry.GetSourcesForContext(context).Select(source => source.Key));
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
