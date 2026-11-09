using dorks_and_dice_site.Modes.DorksAndDice;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// Dorks & Dice-owned services and presentation are registered by its mode
/// module, not indirectly as mandatory dependencies of the generic framework.
/// </summary>
public sealed class DorksAndDiceModeRegistrationTests
{
    [Fact]
    public void AddDorksAndDiceModeRegistersOwnedPresentationServices()
    {
        var services = new ServiceCollection();

        services.AddDorksAndDiceMode();

        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ISiteModePresentationModule));
        Assert.Equal(typeof(DorksAndDicePresentationModule), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }
}
