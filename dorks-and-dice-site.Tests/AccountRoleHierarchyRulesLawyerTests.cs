using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// Rules Lawyer authority is a Dorks & Dice scoped role, not a global Identity
/// role. Owner inheritance is deliberate and must not broaden this role to
/// Professional or unrelated normal modes.
/// </summary>
public sealed class AccountRoleHierarchyRulesLawyerTests
{
    [Fact]
    public void RulesLawyerIsDorksAndDiceScopedRole()
    {
        Assert.DoesNotContain(AccountRoles.RulesLawyer, AccountRoleHierarchy.GlobalRoleNames);
        Assert.DoesNotContain(AccountRoles.RulesLawyer, AccountRoleHierarchy.TopLevelGlobalRoles);
        Assert.DoesNotContain(AccountRoles.RulesLawyer, AccountRoles.OwnerManaged);
        Assert.DoesNotContain(AccountRoles.RulesLawyer, AccountRoles.UiAssignable);
        Assert.Contains(
            ScopedAccountRoles.RulesLawyer,
            ScopedAccountRoles.ForScope(AccountRoleScopes.DorksAndDice));
        Assert.DoesNotContain(
            ScopedAccountRoles.RulesLawyer,
            ScopedAccountRoles.ForScope(AccountRoleScopes.Professional));
    }

    [Fact]
    public void OwnerInheritsDorksAndDiceRulesLawyerButOtherGlobalRolesDoNot()
    {
        Assert.True(AccountRoleHierarchy.InheritsScopedRole(
            AccountRoles.Owner,
            AccountRoleScopes.DorksAndDice,
            ScopedAccountRoles.RulesLawyer));
        Assert.False(AccountRoleHierarchy.InheritsScopedRole(
            AccountRoles.Admin,
            AccountRoleScopes.DorksAndDice,
            ScopedAccountRoles.RulesLawyer));
        Assert.False(AccountRoleHierarchy.InheritsScopedRole(
            AccountRoles.Dev,
            AccountRoleScopes.DorksAndDice,
            ScopedAccountRoles.RulesLawyer));
    }

    [Fact]
    public void DirectRulesLawyerClaimIsLimitedToDorksAndDice()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(
                AccountClaimTypes.ScopedRole,
                $"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.RulesLawyer}")
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            AccountRoleScopes.DorksAndDice,
            ScopedAccountRoles.RulesLawyer));
        Assert.False(AccountRoleHierarchy.PrincipalHasScopedRole(
            principal,
            AccountRoleScopes.Professional,
            ScopedAccountRoles.RulesLawyer));
        Assert.False(AccountRoleHierarchy.PrincipalHasGlobalRole(
            principal,
            AccountRoles.RulesLawyer));
    }
}
