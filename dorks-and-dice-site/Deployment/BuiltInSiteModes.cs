using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Deployment composition compatibility facade for the standard modes shipped by this
/// installation. Generic framework code should consume ISiteModeRegistry rather than this
/// named-mode list. The facade remains while legacy enum consumers are migrated.
/// </summary>
public static class BuiltInSiteModes
{
    public static SiteModeDefinition DorksAndDice =>
        dorks_and_dice_site.Modes.DorksAndDice.DorksAndDiceMode.Definition;

    public static SiteModeDefinition Professional =>
        dorks_and_dice_site.Modes.Professional.ProfessionalMode.Definition;

    public static IReadOnlyList<SiteModeDefinition> All { get; } =
    [
        DorksAndDice,
        Professional
    ];

    public static bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition)
    {
        definition = All.FirstOrDefault(candidate => candidate.LegacyMode == mode);
        return definition is not null;
    }
}
