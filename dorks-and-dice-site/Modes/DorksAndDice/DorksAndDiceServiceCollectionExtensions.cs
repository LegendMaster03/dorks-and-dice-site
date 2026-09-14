using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Modes.DorksAndDice;

public static class DorksAndDiceServiceCollectionExtensions
{
    /// <summary>
    /// Registers services owned by the Dorks & Dice normal mode. Future campaign, character,
    /// and other Dorks-specific domain services should be composed here rather than in generic
    /// framework startup code.
    /// </summary>
    public static IServiceCollection AddDorksAndDiceMode(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISiteModePresentationModule, DorksAndDicePresentationModule>();
        return services;
    }
}
