using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class TesterRoleAuthorizationTests
{
    [Fact]
    public void DirectTesterRoleIsModeScoped()
    {
        var principal = PrincipalWithScopedRole(
            BuiltInSiteModes.DorksAndDice.Id,
            ScopedAccountRoles.Tester);

        Assert.True(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            BuiltInSiteModes.DorksAndDice.Id,
            ScopedAccountRoles.Tester));
        Assert.False(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            BuiltInSiteModes.Professional.Id,
            ScopedAccountRoles.Tester));
    }

    [Theory]
    [InlineData("dorks-and-dice")]
    [InlineData("professional")]
    [InlineData("future-mode")]
    public void DevInheritsTesterAcrossRegisteredModeScopes(string modeId)
    {
        var principal = PrincipalWithGlobalRole(AccountRoles.Dev);

        Assert.True(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            modeId,
            ScopedAccountRoles.Tester));
    }

    [Fact]
    public void DevDoesNotInheritEditor()
    {
        var principal = PrincipalWithGlobalRole(AccountRoles.Dev);

        Assert.False(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            BuiltInSiteModes.DorksAndDice.Id,
            ScopedAccountRoles.Editor));
    }

    [Fact]
    public void AdminDoesNotInheritTesterButOwnerDoes()
    {
        var admin = PrincipalWithGlobalRole(AccountRoles.Admin);
        var owner = PrincipalWithGlobalRole(AccountRoles.Owner);

        Assert.False(AccountRoleHierarchy.PrincipalHasScopedRole(
            admin,
            BuiltInSiteModes.DorksAndDice.Id,
            ScopedAccountRoles.Tester));
        Assert.True(AccountRoleHierarchy.PrincipalHasScopedRole(
            owner,
            BuiltInSiteModes.DorksAndDice.Id,
            ScopedAccountRoles.Tester));
    }

    [Fact]
    public void DevHierarchyShowsTesterForEveryBuiltInMode()
    {
        var dev = AccountRoleHierarchy.GetGlobalRole(AccountRoles.Dev);

        Assert.Equal(
            BuiltInSiteModes.All.Select(mode => $"{mode.DisplayName} {ScopedAccountRoles.Tester}"),
            dev.Children.Select(child => child.DisplayName));
        Assert.All(dev.Children, child => Assert.Equal(ScopedAccountRoles.Tester, child.ScopedRole));
    }

    private static ClaimsPrincipal PrincipalWithScopedRole(string scope, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "tester-user"),
            new Claim(AccountClaimTypes.ScopedRole, $"{scope}:{role}")
        ],
        authenticationType: "test"));

    private static ClaimsPrincipal PrincipalWithGlobalRole(string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "global-role-user"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "test"));
}
