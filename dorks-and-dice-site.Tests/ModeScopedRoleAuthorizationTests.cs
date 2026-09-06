using System.Security.Claims;
using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    public async Task SyntheticModeEditorAuthorizesFromStableModeIdWithoutEnumValue()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var user = CreateScopedEditor($"test-mode:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = syntheticMode
        }));
        Assert.False(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        }));
    }

    [Fact]
    public async Task GlobalEditorAuthorizesSyntheticModeWithoutEnumValue()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);

        Assert.True(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = syntheticMode
        }));
    }

    [Fact]
    public async Task AdminInheritsGlobalEditorAuthorizationIncludingTrustedPreview()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Admin);

        foreach (var editorRole in SiteModeEditorRoles.All)
        {
            Assert.True(await IsAuthorizedAsync(user, editorRole.SiteMode));
        }

        Assert.True(await IsAuthorizedAsync(user, TrustedPreviewContext()));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task GlobalEditorAuthorizesTrustedPreviewWithoutConcreteActiveMode()
    {
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);

        foreach (var editorRole in SiteModeEditorRoles.All)
        {
            Assert.True(await IsAuthorizedAsync(user, editorRole.SiteMode));
        }

        Assert.True(await IsAuthorizedAsync(user, TrustedPreviewContext()));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task ScopedEditorDoesNotGainCrossModeAuthorityInTrustedPreview()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.Editor}");

        Assert.False(await IsAuthorizedAsync(user, TrustedPreviewContext()));
    }

    [Fact]
    public void EditorRootRedirectsGlobalEditorFromTrustedPreviewToContent()
    {
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = TrustedPreviewContext();
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void EditorRootStillForbidsDevOnlyUserInTrustedPreview()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Dev);
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = TrustedPreviewContext();
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        Assert.IsType<ForbidResult>(controller.Index());
    }

    [Fact]
    public void EveryBuiltInModeAutomaticallyDefinesAChildEditorRole()
    {
        Assert.Equal(
            BuiltInSiteModes.All.Select(mode => mode.Id),
            SiteModeEditorRoles.All.Select(role => role.Scope));
        Assert.Equal(
            BuiltInSiteModes.All.Select(mode => mode.LegacyMode),
            SiteModeEditorRoles.All.Select(role => role.LegacySiteMode));

        var globalEditor = AccountRoleHierarchy.GetGlobalRole(AccountRoles.GlobalEditor);
        Assert.Equal(
            SiteModeEditorRoles.All.Select(role => role.RoleName),
            globalEditor.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public void SyntheticModeAutomaticallyGetsEditorRoleWithoutIdentityConfigurationChange()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");

        var role = Assert.Single(SiteModeEditorRoleFactory.Create([syntheticMode]));

        Assert.Equal("test-mode", role.Scope);
        Assert.Equal("Test Mode Editor", role.RoleName);
        Assert.Null(role.LegacySiteMode);
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
        Assert.False(await IsAuthorizedAsync(user, TrustedPreviewContext()));
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

    private static SiteModeContext TrustedPreviewContext() => new()
    {
        FrameworkState = FrameworkRuntimeStates.TrustedPreview,
        HasTrustedAccess = true
    };

    private static Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, SiteMode siteMode) =>
        IsAuthorizedAsync(user, new SiteModeContext
        {
            SiteMode = siteMode
        });

    private static async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, SiteModeContext modeContext)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SiteModeContext.HttpContextItemKey] = modeContext;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var handler = new ModeScopedRoleAuthorizationHandler(accessor);
        var requirement = new ModeScopedRoleRequirement(ScopedAccountRoles.Editor);
        var authorizationContext = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(authorizationContext);
        return authorizationContext.HasSucceeded;
    }
}
