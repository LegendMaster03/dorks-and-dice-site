using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Legacy compatibility facade for callers that still refer to the two current modes by name.
/// DeploymentSiteModeRegistrationSource is the single deployment composition source; this type
/// must not become a second registration list. Generic framework code should consume
/// ISiteModeRegistry instead.
/// </summary>
public static class BuiltInSiteModes
{
    private static readonly IReadOnlyList<SiteModeDefinition> Definitions =
        new DeploymentSiteModeRegistrationSource().GetDefinitions();

    public static SiteModeDefinition DorksAndDice => GetByLegacyMode(SiteMode.DorksAndDice);

    public static SiteModeDefinition Professional => GetByLegacyMode(SiteMode.Professional);

    public static IReadOnlyList<SiteModeDefinition> All => Definitions;

    public static bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition)
    {
        definition = Definitions.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }

    private static SiteModeDefinition GetByLegacyMode(SiteMode mode) =>
        TryGetByLegacyMode(mode, out var definition)
            ? definition!
            : throw new InvalidOperationException(
                $"The current deployment does not register legacy site mode '{mode}'.");
}
