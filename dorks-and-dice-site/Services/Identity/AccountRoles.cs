namespace dorks_and_dice_site.Services.Identity;

public static class AccountRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string GlobalEditor = "Global Editor";
    public const string Dev = "Dev";

    // Admin and Dev are security-sensitive delegated roles that only Owner may manage.
    public static IReadOnlyList<string> OwnerManaged { get; } = [Admin, Dev];

    // Global Editor is a content-authoring role that Admin may manage.
    public static IReadOnlyList<string> AdminManaged { get; } = [GlobalEditor];

    // Global roles exposed through account management. Owner itself remains server-managed.
    public static IReadOnlyList<string> UiAssignable { get; } = [Admin, GlobalEditor, Dev];

    // Compatibility list for the existing privileged-role test/setup helpers.
    public static IReadOnlyList<string> Privileged { get; } = [Admin, Dev];

    // Roles whose authority is stripped when Trusted Access is unavailable.
    // Global Editor intentionally behaves like Editor rather than Admin/Dev.
    public static IReadOnlyList<string> TrustedPrivileged { get; } = [Owner, Admin, Dev];

    public static IReadOnlyList<string> InheritedGlobalRoles(string role) => role switch
    {
        Owner => [Admin, Dev],
        Admin => [GlobalEditor],
        _ => []
    };
}
