namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Global account display-name claim and mode-scoped role claim identifiers.
/// These claims have separate authorization meanings.
/// </summary>
public static class AccountClaimTypes
{
    public const string DisplayName = "display_name";
    public const string ScopedRole = "scoped_role";
}
