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
    public async Task SyntheticNormalModeEditorAuthorizesFromStableModeIdWithoutEnumValue()
    {
        var syntheticNormalMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var user = CreateScopedEditor($"test-mode:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = syntheticNormalMode
        }));
        Assert.False(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        }));
    }

    [Fact]
    public async Task GlobalEditorAuthorizesSyntheticNormalModeWithoutEnumValue()
    {
        var syntheticNormalMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);

        Assert.True(await IsAuthorizedAsync(user, new SiteModeContext
        {
            ActiveMode = syntheticNormalMode
        }));
    }

    [Fact]
    public async Task AdminInheritsGlobalEditorAuthorizationForSyntheticDevelopment()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Admin);

        foreach (var editorRole in SiteModeEditorRoles.All)
        {
            Assert.True(await IsAuthorizedAsync(user, editorRole.SiteMode));
        }

        Assert.True(await IsAuthorizedAsync(user, SyntheticDevelopmentContext()));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task GlobalEditorAuthorizesSyntheticDevelopmentWithoutPreviewTarget()
    {
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);

        Assert.True(await IsAuthorizedAsync(user, SyntheticDevelopmentContext()));
    }

    [Fact]
    public async Task SyntheticDevelopmentPreservesSelectedModeScopedEditorAccess()
    {
        var globalEditor = CreateGlobalRoleUser(AccountRoles.GlobalEditor);
        var professionalEditor = CreateScopedEditor(
            $"{BuiltInSiteModes.Professional.Id}:{ScopedAccountRoles.Editor}");
        var dorksEditor = CreateScopedEditor(
            $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Editor}");
        var context = SyntheticDevelopmentContext(BuiltInSiteModes.Professional);

        Assert.True(await IsAuthorizedAsync(globalEditor, context));
        Assert.True(await IsAuthorizedAsync(professionalEditor, context));
        Assert.False(await IsAuthorizedAsync(dorksEditor, context));
        Assert.Equal(SiteMode.Development, context.SiteMode);
        Assert.Equal(SyntheticSiteModes.Development.Id, context.RuntimeModeId);
    }

    [Fact]
    public async Task ScopedEditorRequiresSelectedPreviewModeInSyntheticDevelopment()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.Editor}");

        Assert.False(await IsAuthorizedAsync(user, SyntheticDevelopmentContext()));
        Assert.True(await IsAuthorizedAsync(
            user,
            SyntheticDevelopmentContext(BuiltInSiteModes.DorksAndDice)));
        Assert.False(await IsAuthorizedAsync(
            user,
            SyntheticDevelopmentContext(BuiltInSiteModes.Professional)));
    }

    [Fact]
    public void EditorRootRedirectsGlobalEditorFromSyntheticDevelopmentToContent()
    {
        var user = CreateGlobalRoleUser(AccountRoles.GlobalEditor);
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = SyntheticDevelopmentContext();
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void EditorRootRedirectsAuthorizedNormalModeEditorToContent()
    {
        var user = CreateScopedEditor(
            $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Editor}");
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.DorksAndDice
        };
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void EditorRootRedirectsScopedEditorForMatchingDevelopmentPreviewTarget()
    {
        var user = CreateScopedEditor(
            $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Editor}");
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] =
            SyntheticDevelopmentContext(BuiltInSiteModes.DorksAndDice);
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void EditorRootStillForbidsDevOnlyUserInSyntheticDevelopment()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Dev);
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = SyntheticDevelopmentContext();
        var controller = new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        Assert.IsType<ForbidResult>(controller.Index());
    }

    [Fact]
    public void EveryBuiltInNormalModeAutomaticallyDefinesAChildEditorRole()
    {
        Assert.Equal(
            BuiltInSiteModes.All.Select(mode => mode.Id),
            SiteModeEditorRoles.All.Select(role => role.Scope));
        Assert.Equal(
            BuiltInSiteModes.All.Select(mode => mode.LegacyMode),
            SiteModeEditorRoles.All.Select(role => role.LegacySiteMode));
        Assert.DoesNotContain(
            SiteModeEditorRoles.All,
            role => string.Equals(
                role.Scope,
                SyntheticSiteModes.Development.Id,
                StringComparison.Ordinal));

        var globalEditor = AccountRoleHierarchy.GetGlobalRole(AccountRoles.GlobalEditor);
        Assert.Equal(
            SiteModeEditorRoles.All.Select(role => role.RoleName),
            globalEditor.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public void SyntheticNormalModeAutomaticallyGetsEditorRoleWithoutIdentityConfigurationChange()
    {
        var syntheticNormalMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");

        var role = Assert.Single(SiteModeEditorRoleFactory.Create([syntheticNormalMode]));

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
        Assert.False(await IsAuthorizedAsync(user, SyntheticDevelopmentContext()));
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

    private static SiteModeContext SyntheticDevelopmentContext(SiteModeDefinition? previewMode = null) => new()
    {
        ActiveMode = previewMode,
        FrameworkState = SyntheticSiteModes.Development,
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
