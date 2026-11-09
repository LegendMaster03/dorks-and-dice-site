namespace dorks_and_dice_site.Models.Site;

/// <summary>
/// Legacy fixed set of the current normal modes and framework runtime states.
/// Generic code should use registered mode IDs and synthetic-state contracts
/// so future modes do not require extending this enum.
/// </summary>
public enum SiteMode
{
    Unassigned,
    DorksAndDice,
    Professional,
    Development
}
