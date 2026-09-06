using System.Security.Claims;
using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Tests;

public sealed class EditorControllerTests
{
    [Fact]
    public void SyntheticScopedEditorRedirectsDirectlyToModeAuthoring()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var controller = CreateController(
            CreateScopedEditor("test-mode"),
            new SiteModeContext { ActiveMode = syntheticMode });

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void TrustedDeveloperRedirectsDirectlyToCentralAuthoring()
    {
        var controller = CreateController(
            CreateGlobalRolePrincipal(AccountRoles.Dev),
            new SiteModeContext
            {
                FrameworkState = FrameworkRuntimeStates.TrustedPreview,
                HasTrustedAccess = true,
                IsDevelopmentPreview = true
            });

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/development/content", result.Url);
    }

    [Fact]
    public void TrustedDeveloperUsesCentralAuthoringEvenWhenPreviewingANormalMode()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var controller = CreateController(
            CreateGlobalRolePrincipal(AccountRoles.Dev),
            new SiteModeContext
            {
                ActiveMode = syntheticMode,
                FrameworkState = FrameworkRuntimeStates.TrustedPreview,
                HasTrustedAccess = true,
                IsDevelopmentPreview = true
            });

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/development/content", result.Url);
    }

    [Fact]
    public void TrustedPreviewScopedEditorWithoutDeveloperRoleStaysModeBound()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var controller = CreateController(
            CreateScopedEditor("test-mode"),
            new SiteModeContext
            {
                ActiveMode = syntheticMode,
                FrameworkState = FrameworkRuntimeStates.TrustedPreview,
                HasTrustedAccess = true
            });

        var result = Assert.IsType<RedirectResult>(controller.Index());

        Assert.Equal("/editor/content", result.Url);
    }

    [Fact]
    public void EditorEntryRequiresAuthorizedAuthoringAccess()
    {
        var controller = CreateController(
            CreateScopedEditor("test-mode"),
            new SiteModeContext
            {
                FrameworkState = FrameworkRuntimeStates.TrustedPreview,
                HasTrustedAccess = true
            });

        Assert.IsType<ForbidResult>(controller.Index());
    }

    private static EditorController CreateController(
        ClaimsPrincipal user,
        SiteModeContext modeContext)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = modeContext;
        return new EditorController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ClaimsPrincipal CreateScopedEditor(string scope)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "editor-user"),
            new Claim(AccountClaimTypes.ScopedRole, $"{scope}:{ScopedAccountRoles.Editor}")
        ],
        authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateGlobalRolePrincipal(string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "developer-user"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "test",
        nameType: ClaimTypes.Name,
        roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
