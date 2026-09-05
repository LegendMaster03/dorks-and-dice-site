using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeHomeServiceTests
{
    [Fact]
    public async Task DatabaseBackedHomepagePrecedesCompiledModeHomeModule()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var homepage = new HomepageContentViewModel
        {
            Item = new ContentItem { Id = "home", Slug = "home", Title = "Home" },
            Fragments = [ContentPageFragment.Html("<p>Database homepage</p>")]
        };
        var service = new SiteModeHomeService(
            new TestHomepageContentService(homepage),
        [
            new TestHomeModule(FrameworkRuntimeStates.Fallback.Id, "~/fallback.cshtml"),
            new TestHomeModule(syntheticMode.Id, "~/test-mode.cshtml")
        ]);

        var result = await service.GetHomeAsync(new SiteModeContext
        {
            ActiveMode = syntheticMode
        });

        Assert.Equal("~/Views/Content/Homepage.cshtml", result.ViewPath);
        Assert.Same(homepage, result.Model);
    }

    [Fact]
    public async Task SyntheticModeResolvesHomeWithoutLegacyEnumValue()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var service = new SiteModeHomeService(
            new TestHomepageContentService(),
        [
            new TestHomeModule(FrameworkRuntimeStates.Fallback.Id, "~/fallback.cshtml"),
            new TestHomeModule(syntheticMode.Id, "~/test-mode.cshtml")
        ]);

        var result = await service.GetHomeAsync(new SiteModeContext
        {
            ActiveMode = syntheticMode
        });

        Assert.Equal("~/test-mode.cshtml", result.ViewPath);
    }

    [Fact]
    public async Task MissingModeHomeUsesFrameworkFallbackModule()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var service = new SiteModeHomeService(
            new TestHomepageContentService(),
        [
            new TestHomeModule(FrameworkRuntimeStates.Fallback.Id, "~/fallback.cshtml")
        ]);

        var result = await service.GetHomeAsync(new SiteModeContext
        {
            ActiveMode = syntheticMode
        });

        Assert.Equal("~/fallback.cshtml", result.ViewPath);
    }

    private sealed class TestHomepageContentService(HomepageContentViewModel? homepage = null) : IHomepageContentService
    {
        public Task<HomepageContentViewModel?> GetAsync(
            SiteModeContext modeContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(homepage);
    }

    private sealed class TestHomeModule(string homeKey, string viewPath) : ISiteModeHomeModule
    {
        public string HomeKey { get; } = homeKey;

        public Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SiteModeHomeResult(viewPath));
    }
}
