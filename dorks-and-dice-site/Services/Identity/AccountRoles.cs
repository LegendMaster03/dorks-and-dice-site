namespace dorks_and_dice_site.Services.Identity;

public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Dev = "Dev";

    public static IReadOnlyList<string> Privileged { get; } = [Admin, Dev];
}
