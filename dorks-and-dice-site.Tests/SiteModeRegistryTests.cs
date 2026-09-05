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
    public void SyntheticModeGetsNormalModeBehaviorWithoutProductionEnumValue()
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
        Assert.True(registered.SupportsContent);
        Assert.True(registered.SupportsScopedEditor);
    }

    [Fact]
    public void FrameworkRuntimeStatesRemainAvailableOnlyAsCompatibilityMetadata()
    {
        Assert.Equal(SiteMode.Unassigned, FrameworkRuntimeStates.Fallback.LegacyMode);
        Assert.Equal("unassigned", FrameworkRuntimeStates.Fallback.Id);
        Assert.Equal(SiteMode.Development, FrameworkRuntimeStates.TrustedPreview.LegacyMode);
        Assert.Equal("development", FrameworkRuntimeStates.TrustedPreview.Id);

        var registry = new SiteModeRegistry(BuiltInSiteModes.All);
        Assert.False(registry.TryGetByLegacyMode(SiteMode.Unassigned, out _));
        Assert.False(registry.TryGetByLegacyMode(SiteMode.Development, out _));
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
