namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Supplies normal hosted mode definitions to the runtime registry. The framework deliberately
/// does not prescribe whether a source is compiled code, configuration, a manifest, or persistent
/// runtime data.
/// </summary>
public interface ISiteModeRegistrationSource
{
    IReadOnlyList<SiteModeDefinition> GetDefinitions();
}
