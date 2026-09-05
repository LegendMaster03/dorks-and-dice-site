using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeHomeServiceTests
{
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
        [
            new TestHomeModule(FrameworkRuntimeStates.Fallback.Id, "~/fallback.cshtml")
        ]);

        var result = await service.GetHomeAsync(new SiteModeContext
        {
            ActiveMode = syntheticMode
        });

        Assert.Equal("~/fallback.cshtml", result.ViewPath);
    }

    private sealed class TestHomeModule(string homeKey, string viewPath) : ISiteModeHomeModule
    {
        public string HomeKey { get; } = homeKey;

        public Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SiteModeHomeResult(viewPath));
    }
}
