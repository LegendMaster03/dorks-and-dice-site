using System.Security.Claims;
using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Models.Editor;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class EditorControllerTests
{
    [Fact]
    public void SyntheticScopedEditorAppearsWithoutNamedModeControllerLogic()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var registry = new SiteModeRegistry([syntheticMode]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SiteHosting:Modes:test-mode:CanonicalHost"] = "test.example.test"
            })
            .Build();
        var controller = CreateController(
            registry,
            new SiteModeOptions(configuration),
            CreateScopedEditor("test-mode"),
            new SiteModeContext { FrameworkState = FrameworkRuntimeStates.Fallback });

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<EditorIndexViewModel>(result.Model);
        var option = Assert.Single(model.Modes);

        Assert.Equal("test-mode", option.ModeId);
        Assert.Equal("Test Mode", option.DisplayName);
        Assert.Equal("https://test.example.test/editor/content", option.EditorHref);
    }

    [Fact]
    public void TrustedPreviewUsesModeSelectionInsteadOfPublicHostLink()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var controller = CreateController(
            new SiteModeRegistry([syntheticMode]),
            new SiteModeOptions(),
            CreateScopedEditor("test-mode"),
            new SiteModeContext
            {
                ActiveMode = syntheticMode,
                FrameworkState = FrameworkRuntimeStates.TrustedPreview,
                HasTrustedAccess = true
            });

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<EditorIndexViewModel>(result.Model);
        var option = Assert.Single(model.Modes);

        Assert.True(model.IsTrustedPreview);
        Assert.Null(option.EditorHref);
    }

    private static EditorController CreateController(
        ISiteModeRegistry registry,
        SiteModeOptions options,
        ClaimsPrincipal user,
        SiteModeContext modeContext)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Items[SiteModeContext.HttpContextItemKey] = modeContext;
        return new EditorController(registry, options)
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
}
