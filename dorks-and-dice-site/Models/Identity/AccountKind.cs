namespace dorks_and_dice_site.Models.Identity;

/// <summary>
/// Distinguishes human accounts eligible for normal interactive sign-in
/// from Operator service principals using dedicated credentials.
/// </summary>
public enum AccountKind
{
    Human = 0,
    ServicePrincipal = 1
}
