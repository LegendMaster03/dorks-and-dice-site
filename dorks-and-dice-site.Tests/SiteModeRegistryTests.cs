using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

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
    public void SyntheticStandardModeGetsNormalModeBehaviorWithoutProductionEnumValue()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var registry = new SiteModeRegistry(BuiltInSiteModes.All.Append(syntheticMode));

        var registered = registry.GetById("test-mode");

        Assert.Same(syntheticMode, registered);
        Assert.Null(registered.LegacyMode);
        Assert.Equal(SiteModeKind.Standard, registered.Kind);
        Assert.True(registered.IsStandardMode);
        Assert.True(registered.SupportsContent);
        Assert.True(registered.SupportsScopedEditor);
    }

    [Fact]
    public void UnassignedIsFrameworkFallbackRatherThanNormalSiteMode()
    {
        Assert.Equal(SiteModeKind.Fallback, BuiltInSiteModes.Unassigned.Kind);
        Assert.True(BuiltInSiteModes.Unassigned.IsFallback);
        Assert.False(BuiltInSiteModes.Unassigned.IsStandardMode);
        Assert.False(BuiltInSiteModes.Unassigned.SupportsContent);
        Assert.False(BuiltInSiteModes.Unassigned.SupportsScopedEditor);
    }

    [Fact]
    public void DevelopmentRepresentsGlobalTrustedPreviewRatherThanNormalSiteMode()
    {
        Assert.Equal(SiteModeKind.TrustedPreview, BuiltInSiteModes.Development.Kind);
        Assert.True(BuiltInSiteModes.Development.IsTrustedPreview);
        Assert.False(BuiltInSiteModes.Development.IsStandardMode);
        Assert.False(BuiltInSiteModes.Development.SupportsContent);
        Assert.False(BuiltInSiteModes.Development.SupportsScopedEditor);
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
