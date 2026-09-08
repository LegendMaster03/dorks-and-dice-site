using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Tests;

public sealed class AccountRoleHierarchyRulesLawyerTests
{
    [Fact]
    public void RulesLawyerIsIndependentTopLevelRoleOwnedByOwner()
    {
        Assert.Contains(AccountRoles.RulesLawyer, AccountRoleHierarchy.TopLevelGlobalRoles);
        Assert.Contains(AccountRoles.RulesLawyer, AccountRoles.OwnerManaged);
        Assert.Contains(AccountRoles.RulesLawyer, AccountRoles.UiAssignable);
        Assert.DoesNotContain(AccountRoles.RulesLawyer, AccountRoles.TrustedPrivileged);
        Assert.Empty(AccountRoleHierarchy.GetGlobalRole(AccountRoles.RulesLawyer).Children);
    }

    [Fact]
    public void OwnerAutomaticallyInheritsEveryTopLevelGlobalRole()
    {
        Assert.DoesNotContain(AccountRoles.Owner, AccountRoleHierarchy.TopLevelGlobalRoles);
        Assert.DoesNotContain(AccountRoles.GlobalEditor, AccountRoleHierarchy.TopLevelGlobalRoles);

        foreach (var role in AccountRoleHierarchy.TopLevelGlobalRoles)
        {
            Assert.True(
                AccountRoleHierarchy.InheritsGlobalRole(AccountRoles.Owner, role),
                $"Owner did not inherit top-level role '{role}'.");
        }

        Assert.True(AccountRoleHierarchy.InheritsGlobalRole(AccountRoles.Owner, AccountRoles.RulesLawyer));
        Assert.True(AccountRoleHierarchy.InheritsGlobalRole(AccountRoles.Owner, AccountRoles.GlobalEditor));
    }
}
