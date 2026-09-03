using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class InitialContentMediaIntegrationTests
{
    [Fact]
    public void PublicModesAllowManagedMediaRequestsToReachVisibilityEnforcement()
    {
        const string path = "/content/media/0123456789abcdef0123456789abcdef/image.png";
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Professional));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.DorksAndDice));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Unassigned));
    }
}
