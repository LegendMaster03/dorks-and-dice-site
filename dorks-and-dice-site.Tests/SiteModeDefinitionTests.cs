using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// Anonymous account-navigation visibility is mode metadata, not a hard-coded
/// Professional-versus-Dorks switch in the generic layout.
/// </summary>
public sealed class SiteModeDefinitionTests
{
    [Fact]
    public void SyntheticModeAdvertisesAnonymousLoginByDefault()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");

        Assert.True(syntheticMode.ShowAnonymousLoginInNavigation);
    }

    [Fact]
    public void ProfessionalModeCanSuppressAnonymousLoginWithoutSharedViewSwitch()
    {
        Assert.False(BuiltInSiteModes.Professional.ShowAnonymousLoginInNavigation);
        Assert.True(BuiltInSiteModes.DorksAndDice.ShowAnonymousLoginInNavigation);
    }
}
