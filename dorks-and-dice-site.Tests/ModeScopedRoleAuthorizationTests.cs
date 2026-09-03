using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class ModeScopedRoleAuthorizationTests
{
    [Fact]
    public async Task DorksAndDiceEditorIsAuthorizedOnlyInDorksAndDiceMode()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Professional));
    }

    [Fact]
    public async Task ProfessionalEditorIsAuthorizedOnlyInProfessionalMode()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, SiteMode.Professional));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
    }

    [Fact]
    public async Task AdminInheritsGlobalEditorAuthorizationFromHierarchy()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Admin);

        foreach (var editorRole in SiteModeEditorRoles.All)
        {
            Assert.True(await IsAuthorizedAsync(user, editorRole.SiteMode));
        }

        Assert.True(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task GlobalEditorAuthorizesSharedEditorRoutesInEveryPreviewMode()
    {
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);

        foreach (var editorRole in SiteModeEditorRoles.All)
        {
            Assert.True(await IsAuthorizedAsync(user, editorRole.SiteMode));
        }

        Assert.True(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public void EveryContentSiteModeAutomaticallyDefinesAChildEditorRole()
    {
        var expectedModes = Enum.GetValues<SiteMode>()
            .Where(SiteModeValues.IsEditorMode)
            .ToArray();

        Assert.Equal(expectedModes, SiteModeEditorRoles.All.Select(role => role.SiteMode));
        var globalEditor = AccountRoleHierarchy.GetGlobalRole(AccountRoles.GlobalEditor);
        Assert.Equal(
            SiteModeEditorRoles.All.Select(role => role.RoleName),
            globalEditor.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public void RoleHierarchyIsRecursiveAndIsTheInheritanceSourceOfTruth()
    {
        var owner = AccountRoleHierarchy.GetGlobalRole(AccountRoles.Owner);
        var admin = owner.Children.Single(child => child.GlobalRole == AccountRoles.Admin);
        var dev = owner.Children.Single(child => child.GlobalRole == AccountRoles.Dev);
        var globalEditor = admin.Children.Single(child => child.GlobalRole == AccountRoles.GlobalEditor);

        Assert.Empty(dev.Children);
        Assert.Equal(
            SiteModeEditorRoles.All.Select(role => role.RoleName),
            globalEditor.Children.Select(child => child.DisplayName));
        Assert.Contains(AccountRoles.Admin, AccountRoleHierarchy.GetInheritedGlobalRoles(AccountRoles.Owner));
        Assert.Contains(AccountRoles.GlobalEditor, AccountRoleHierarchy.GetInheritedGlobalRoles(AccountRoles.Owner));
        Assert.Contains(AccountRoles.Dev, AccountRoleHierarchy.GetInheritedGlobalRoles(AccountRoles.Owner));
    }

    [Fact]
    public async Task DevRoleDoesNotSatisfyEditorRequirement()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Dev);

        Assert.False(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
    }

    private static ClaimsPrincipal CreateScopedEditor(string scopedRole)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(AccountClaimTypes.ScopedRole, scopedRole)
        ],
        authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateGlobalRoleUser(string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "global-role-user"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, SiteMode siteMode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            SiteMode = siteMode
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var handler = new ModeScopedRoleAuthorizationHandler(accessor);
        var requirement = new ModeScopedRoleRequirement(ScopedAccountRoles.Editor);
        var authorizationContext = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(authorizationContext);
        return authorizationContext.HasSucceeded;
    }
}
