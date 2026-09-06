using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeRegistryTests
{
    [Fact]
    public void BuiltInModesResolveByStableIdAndLegacyMode()
    {
        var registry = new SiteModeRegistry(BuiltInSiteModes.All);

        Assert.Same(BuiltInSiteModes.DorksAndDice, registry.GetById("dorks-and-dice"));
        Assert.Same(BuiltInSiteModes.Professional, registry.GetByLegacyMode(SiteMode.Professional));
        Assert.Equal("Dorks & Dice", registry.GetByLegacyMode(SiteMode.DorksAndDice).DisplayName);
    }

    [Fact]
    public void BuiltInRegistryContainsOnlyNormalHostedModes()
    {
        var registry = new SiteModeRegistry(BuiltInSiteModes.All);

        Assert.Equal(2, registry.All.Count);
        Assert.Contains(BuiltInSiteModes.DorksAndDice, registry.All);
        Assert.Contains(BuiltInSiteModes.Professional, registry.All);
        Assert.DoesNotContain(registry.All, definition => definition.LegacyMode == SiteMode.Unassigned);
        Assert.DoesNotContain(registry.All, definition => definition.LegacyMode == SiteMode.Development);
    }

    [Fact]
    public void RegisteredModeWithoutLegacyEnumGetsNormalModeBehavior()
    {
        var testMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var registry = new SiteModeRegistry(BuiltInSiteModes.All.Append(testMode));

        var registered = registry.GetById("test-mode");

        Assert.Same(testMode, registered);
        Assert.Null(registered.LegacyMode);
        Assert.True(registered.SupportsContent);
        Assert.True(registered.SupportsScopedEditor);
        Assert.Equal(["/", "/articles"], registered.SitemapPaths);
    }

    [Fact]
    public void RegisteredModeWithoutLegacyEnumOwnsRoutesAndAssets()
    {
        var testMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-assets")
        {
            OwnedRoutePrefixes = ["/test-area"]
        };

        Assert.True(SiteRouteOwnership.IsAllowedInMode("/", testMode));
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/articles/example", testMode));
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/test-area", testMode));
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/test-area/nested", testMode));
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/site-modes/test-assets/css/site.css", testMode));
        Assert.False(SiteRouteOwnership.IsAllowedInMode("/resume", testMode));
        Assert.False(SiteRouteOwnership.IsAllowedInMode("/site-modes/professional/css/site.css", testMode));
    }

    [Fact]
    public void FrameworkFallbackAndSyntheticDevelopmentStayOutsideNormalRegistry()
    {
        Assert.Equal(SiteMode.Unassigned, FrameworkRuntimeStates.Fallback.LegacyMode);
        Assert.Equal("unassigned", FrameworkRuntimeStates.Fallback.Id);
        Assert.Same(SyntheticSiteModes.Development, FrameworkRuntimeStates.TrustedPreview);
        Assert.Equal(SiteMode.Development, SyntheticSiteModes.Development.LegacyMode);
        Assert.Equal("development", SyntheticSiteModes.Development.Id);

        var registry = new SiteModeRegistry(BuiltInSiteModes.All);
        Assert.False(registry.TryGetByLegacyMode(SiteMode.Unassigned, out _));
        Assert.False(registry.TryGetByLegacyMode(SiteMode.Development, out _));
    }

    [Fact]
    public async Task SyntheticDevelopmentCanPreviewRegisteredModeWithoutEnumValue()
    {
        var testMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var registry = new SiteModeRegistry(BuiltInSiteModes.All.Append(testMode));
        SiteModeContext? captured = null;
        var middleware = new SiteModeMiddleware(
            context =>
            {
                captured = context.GetSiteModeContext();
                return Task.CompletedTask;
            },
            new SiteModeOptions(),
            registry);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");
        context.Request.Headers.Cookie = $"{SiteModeValues.DevelopmentSiteModeCookie}=test-mode";

        await middleware.InvokeAsync(context);

        Assert.NotNull(captured);
        Assert.Same(testMode, captured.ActiveMode);
        Assert.Equal("test-mode", captured.ActiveModeId);
        Assert.Same(SyntheticSiteModes.Development, captured.SyntheticMode);
        Assert.True(captured.IsTrustedPreview);
        Assert.False(captured.IsFrameworkFallback);
        Assert.Equal(SiteMode.Development, captured.SiteMode);
        Assert.Equal(SyntheticSiteModes.Development.Id, captured.RuntimeModeId);
    }

    [Fact]
    public void SyntheticDevelopmentAddsFrameworkAssetsWithoutExpandingSelectedLiveModeRoutes()
    {
        var selectedMode = BuiltInSiteModes.DorksAndDice;
        const string developmentAsset = "/site-modes/development/css/site.css";

        Assert.False(SiteRouteOwnership.IsAllowedInMode(developmentAsset, selectedMode));
        Assert.True(SiteRouteOwnership.IsAllowedInSyntheticMode(
            developmentAsset,
            SyntheticSiteModes.Development,
            selectedMode));
        Assert.False(SiteRouteOwnership.IsAllowedInSyntheticMode(
            "/resume",
            SyntheticSiteModes.Development,
            selectedMode));
    }

    [Fact]
    public async Task UnknownHostUsesFrameworkFallbackWithoutCreatingAHostedMode()
    {
        SiteModeContext? captured = null;
        var middleware = new SiteModeMiddleware(
            context =>
            {
                captured = context.GetSiteModeContext();
                return Task.CompletedTask;
            },
            new SiteModeOptions(),
            new SiteModeRegistry(BuiltInSiteModes.All));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("unassigned.example");

        await middleware.InvokeAsync(context);

        Assert.NotNull(captured);
        Assert.Null(captured.ActiveMode);
        Assert.Null(captured.ActiveModeId);
        Assert.Same(FrameworkRuntimeStates.Fallback, captured.FrameworkState);
        Assert.True(captured.IsFrameworkFallback);
        Assert.False(captured.IsTrustedPreview);
        Assert.Equal(SiteMode.Unassigned, captured.SiteMode);
    }

    [Fact]
    public void StylesheetResolverUsesModeAssetMetadataForRegisteredModeWithoutEnum()
    {
        var testMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "custom-test-assets");
        var resolver = new SiteModeStylesheetResolver();

        var paths = resolver.GetStylesheetPaths(new SiteModeContext
        {
            ActiveMode = testMode
        });

        Assert.Equal(["~/site-modes/custom-test-assets/css/site.css"], paths);
    }

    [Fact]
    public void SyntheticDevelopmentStylesheetIsAnOverlayOnSelectedMode()
    {
        var resolver = new SiteModeStylesheetResolver();

        var paths = resolver.GetStylesheetPaths(new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.DorksAndDice,
            FrameworkState = SyntheticSiteModes.Development
        });

        Assert.Equal(
            [
                "~/site-modes/dorks-and-dice/css/site.css",
                "~/site-modes/development/css/site.css"
            ],
            paths);
    }

    [Fact]
    public void DuplicateStableIdsAreRejected()
    {
        var duplicate = BuiltInSiteModes.DorksAndDice with { LegacyMode = null };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new SiteModeRegistry(BuiltInSiteModes.All.Append(duplicate)));

        Assert.Contains("Duplicate site mode id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateLegacyModesAreRejected()
    {
        var duplicate = new SiteModeDefinition(
            Id: "another-professional",
            DisplayName: "Another Professional",
            LegacyMode: SiteMode.Professional,
            ViewFolder: "AnotherProfessional",
            AssetFolder: "another-professional");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new SiteModeRegistry(BuiltInSiteModes.All.Append(duplicate)));

        Assert.Contains("Duplicate legacy site mode", exception.Message, StringComparison.Ordinal);
    }
}
