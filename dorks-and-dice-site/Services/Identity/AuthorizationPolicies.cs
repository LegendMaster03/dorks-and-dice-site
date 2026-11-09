namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Names the shared authorization policies for global administrative access,
/// trusted development controls, and normal-mode-scoped editing.
/// </summary>
public static class AuthorizationPolicies
{
    public const string TrustedAccess = "TrustedAccess";
    public const string AdminAccess = "AdminAccess";
    public const string OwnerAccess = "OwnerAccess";
    public const string DevAccess = "DevAccess";
    public const string PrivilegedAccess = "PrivilegedAccess";
    public const string AdminAndDevAccess = "AdminAndDevAccess";
    public const string ModeEditor = "ModeEditor";
}
