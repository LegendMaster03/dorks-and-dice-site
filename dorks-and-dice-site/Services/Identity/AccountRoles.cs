namespace dorks_and_dice_site.Services.Identity;

public static class AccountRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Dev = "Dev";

    // Admin and Dev are the delegated global roles an Owner may manage through the UI.
    public static IReadOnlyList<string> Privileged { get; } = [Admin, Dev];

    // All global roles whose authority is restricted to Trusted Access.
    public static IReadOnlyList<string> TrustedPrivileged { get; } = [Owner, Admin, Dev];
}
