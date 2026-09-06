using System.Security.Claims;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringNavigationTests
{
    [Fact]
    public void HomepagePublicRouteUsesModeRoot()
    {
        Assert.Equal(
            "/",
            ContentPublicRoute.GetPath("professional-home", [ContentTags.Homepage]));
    }

    [Fact]
    public void UnsavedContentHasNoPublicRouteYet()
    {
        Assert.Equal(string.Empty, ContentPublicRoute.GetPath(string.Empty, [ContentTags.Article]));
    }

    [Fact]
    public void ExperienceOnlyPublicRoutePreservesExperienceContext()
    {
        Assert.Equal(
            "/resume/cybersecurity?context=experience",
            ContentPublicRoute.GetPath("cybersecurity", [ContentTags.Experience]));
    }

    [Fact]
    public void HostedModeEditorUsesOnlyAuthoringWorkspace()
    {
        using var fixture = new SourceFixture();
        var modeContext = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        };

        var sources = ContentAuthoringSourceAccess.GetModeEditorSources(fixture.Registry, modeContext);

        Assert.Equal(["Local"], sources.Select(source => source.Key));
        Assert.Equal(
            "Local",
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, null));
        Assert.Throws<InvalidOperationException>(() =>
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, "External"));
    }

    [Fact]
    public void SyntheticDevelopmentEditorUsesOnlySelectedDatabaseSources()
    {
        using var fixture = new SourceFixture();
        var modeContext = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(["External"], StringComparer.OrdinalIgnoreCase)
        };

        var sources = ContentAuthoringSourceAccess.GetModeEditorSources(fixture.Registry, modeContext);

        Assert.Equal(["External"], sources.Select(source => source.Key));
        Assert.Equal(
            "External",
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, null));
        Assert.Equal(
            "External",
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, "external"));
        Assert.Throws<InvalidOperationException>(() =>
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, "Local"));
    }

    [Fact]
    public void SyntheticDevelopmentEditorWithNoDatabaseSelectionHasNoFallbackSource()
    {
        using var fixture = new SourceFixture();
        var modeContext = new SiteModeContext
        {
            FrameworkState = SyntheticSiteModes.Development,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        Assert.Empty(ContentAuthoringSourceAccess.GetModeEditorSources(fixture.Registry, modeContext));
        Assert.Throws<InvalidOperationException>(() =>
            ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(fixture.Registry, modeContext, null));
    }

    [Fact]
    public void CentralAuthoringCanUseAllConfiguredSources()
    {
        using var fixture = new SourceFixture();

        var sources = ContentAuthoringSourceAccess.GetCentralSources(fixture.Registry);

        Assert.Equal(["Local", "External", "Hidden"], sources.Select(source => source.Key));
        Assert.Equal(
            "External",
            ContentAuthoringSourceAccess.ResolveCentralSourceKey(fixture.Registry, "external"));
    }

    [Fact]
    public void ModeEditorCanEditOnlyAuthorizedActiveModeContent()
    {
        var dorksModeId = BuiltInSiteModes.DorksAndDice.Id;
        var professionalModeId = BuiltInSiteModes.Professional.Id;
        var principal = PrincipalWithScopedEditor(dorksModeId);

        Assert.True(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [dorksModeId] },
            dorksModeId));
        Assert.False(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [professionalModeId] },
            dorksModeId));
        Assert.False(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [dorksModeId, professionalModeId] },
            dorksModeId));
    }

    [Fact]
    public void GlobalEditorInSelectedPreviewModeRemainsModeScoped()
    {
        var principal = PrincipalWithGlobalRole(AccountRoles.GlobalEditor);
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true,
            IsDevelopmentPreview = true
        };

        Assert.True(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [BuiltInSiteModes.Professional.Id] },
            context));
        Assert.False(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [BuiltInSiteModes.DorksAndDice.Id] },
            context));
        Assert.False(ContentAuthoringModeAccess.CanSelectModes(principal, context));
    }

    [Fact]
    public void GlobalEditorAtDevelopmentRootCanEditAcrossModesAndSelectModes()
    {
        var principal = PrincipalWithGlobalRole(AccountRoles.GlobalEditor);
        var context = new SiteModeContext
        {
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true,
            IsDevelopmentPreview = true
        };
        var item = new ContentItem
        {
            VisibleInModes = [BuiltInSiteModes.DorksAndDice.Id, BuiltInSiteModes.Professional.Id]
        };

        Assert.True(ContentAuthoringModeAccess.CanEditItem(principal, item, context));
        Assert.True(ContentAuthoringModeAccess.CanSelectModes(principal, context));
    }

    [Fact]
    public void ScopedEditorInSelectedDevelopmentPreviewRemainsScopedToThatMode()
    {
        var principal = PrincipalWithScopedEditor(BuiltInSiteModes.Professional.Id);
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true
        };

        Assert.True(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [BuiltInSiteModes.Professional.Id] },
            context));
        Assert.False(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [BuiltInSiteModes.DorksAndDice.Id] },
            context));
        Assert.False(ContentAuthoringModeAccess.CanSelectModes(principal, context));
    }

    [Fact]
    public void OwnerInheritanceAuthorizesSharedModeContentWithoutDirectEditorClaims()
    {
        var dorksModeId = BuiltInSiteModes.DorksAndDice.Id;
        var professionalModeId = BuiltInSiteModes.Professional.Id;
        var principal = PrincipalWithGlobalRole(AccountRoles.Owner);

        Assert.True(ContentAuthoringModeAccess.CanEditItem(
            principal,
            new ContentItem { VisibleInModes = [dorksModeId, professionalModeId] },
            dorksModeId));
    }

    private static ClaimsPrincipal PrincipalWithScopedEditor(string modeId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(AccountClaimTypes.ScopedRole, $"{modeId}:{ScopedAccountRoles.Editor}")],
            authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal PrincipalWithGlobalRole(string role)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private sealed class SourceFixture : IDisposable
    {
        private readonly string _directory;

        public SourceFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-authoring-navigation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalDb"] = "Data Source=local.db",
                ["ConnectionStrings:ExternalDb"] = "Data Source=external.db",
                ["ConnectionStrings:HiddenDb"] = "Data Source=hidden.db",
                ["ContentStorage:AuthoringSource"] = "Local",
                ["ContentStorage:Sources:Local:DisplayName"] = "Local content",
                ["ContentStorage:Sources:Local:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Local:ConnectionString"] = "LocalDb",
                ["ContentStorage:Sources:External:DisplayName"] = "External content",
                ["ContentStorage:Sources:External:Provider"] = "Sqlite",
                ["ContentStorage:Sources:External:ConnectionString"] = "ExternalDb",
                ["ContentStorage:Sources:Hidden:DisplayName"] = "Hidden content",
                ["ContentStorage:Sources:Hidden:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Hidden:ConnectionString"] = "HiddenDb",
                ["ContentStorage:GlobalSources:0"] = "External",
                ["ContentStorage:ModeSources:professional:0"] = "External"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
