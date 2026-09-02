using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Identity;

public static class ScopedAccountRoles
{
    public const string Editor = "Editor";

    public static IReadOnlyList<string> All { get; } = [Editor];
}

public static class AccountRoleScopes
{
    public const string DorksAndDice = SiteModeValues.DorksAndDiceModeValue;
    public const string Professional = SiteModeValues.ProfessionalModeValue;

    public static IReadOnlyList<string> All { get; } = [DorksAndDice, Professional];
}
