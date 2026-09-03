namespace dorks_and_dice_site.Services.Identity;

public static class AuthorizationPolicies
{
    public const string TrustedAccess = "TrustedAccess";
    public const string AdminAccess = "AdminAccess";
    public const string DevAccess = "DevAccess";
    public const string PrivilegedAccess = "PrivilegedAccess";
    public const string AdminAndDevAccess = "AdminAndDevAccess";
    public const string ModeEditor = "ModeEditor";
}
