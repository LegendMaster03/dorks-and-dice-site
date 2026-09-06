namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Current deployment composition for normal hosted modes. This is intentionally separate from
/// the framework registry so another deployment can supply a different source without modifying
/// generic framework code.
/// </summary>
public sealed class DeploymentSiteModeRegistrationSource : ISiteModeRegistrationSource
{
    private static readonly IReadOnlyList<SiteModeDefinition> Definitions =
    [
        dorks_and_dice_site.Modes.DorksAndDice.DorksAndDiceMode.Definition,
        dorks_and_dice_site.Modes.Professional.ProfessionalMode.Definition
    ];

    public IReadOnlyList<SiteModeDefinition> GetDefinitions() => Definitions;
}
