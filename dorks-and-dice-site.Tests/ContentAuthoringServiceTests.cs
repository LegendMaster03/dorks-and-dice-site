using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringServiceTests
{
    [Fact]
    public async Task SavingCreatesRevisionAndPreservesStablePageIdentity()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = CreateService(fixture.Registry);
        var newModel = service.GetNew("Test");
        newModel.Document.Id = "authoring-test";
        newModel.Document.Slug = "authoring-test";

        var created = await service.CreateAsync(newModel.Document);
        var editModel = await service.GetEditAsync("Test", created.Slug);

        Assert.NotNull(editModel);
        Assert.Equal("authoring-test", editModel.Document.Id);
        Assert.Single(editModel.History);

        var firstRevisionId = editModel.Document.ExpectedRevisionId;
        editModel.Document.Slug = "authoring-test-moved";
        editModel.Document.Body += "\n\nSecond revision.";
        var saved = await service.SaveRevisionAsync(editModel.Document);

        Assert.Equal("authoring-test", saved.Id);
        Assert.Equal("authoring-test-moved", saved.Slug);
        Assert.NotEqual(firstRevisionId, saved.RevisionId);

        var reloaded = await service.GetEditAsync("Test", "authoring-test-moved");
        Assert.NotNull(reloaded);
        Assert.Equal(saved.RevisionId, reloaded.Document.ExpectedRevisionId);
        Assert.Equal(2, reloaded.History.Count);
        Assert.Equal(firstRevisionId, reloaded.History[0].ParentRevisionId);
        Assert.Contains("Second revision.", reloaded.Document.Body);
        Assert.Null(await fixture.GetBySlugAsync("authoring-test"));

        var redirect = await fixture.ResolveRedirectAsync(
            ContentRouteNamespaces.Articles,
            "authoring-test");
        Assert.NotNull(redirect);
        Assert.Equal("authoring-test", redirect.ContentKey);
    }

    [Fact]
    public async Task SyntheticRegisteredModeCanOwnContentWithoutEnumValue()
    {
        using var fixture = new AuthoringSourceFixture();
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var registry = new SiteModeRegistry(BuiltInSiteModes.All.Append(syntheticMode));
        var service = new ContentAuthoringService(fixture.Registry, registry);
        var model = service.GetNew("Test");
        model.Document.Id = "synthetic-mode-content";
        model.Document.Slug = "synthetic-mode-content";
        model.Document.VisibleModesSelection = [syntheticMode.Id];

        await service.CreateAsync(model.Document);

        var saved = await fixture.GetBySlugAsync(model.Document.Slug);
        Assert.NotNull(saved);
        Assert.Equal([syntheticMode.Id], saved.VisibleInModes);
        Assert.True(saved.IsVisibleInMode(syntheticMode.Id));
        Assert.False(saved.IsVisibleInMode(BuiltInSiteModes.Professional.Id));

        var edit = await service.GetEditAsync("Test", model.Document.Slug);
        Assert.NotNull(edit);
        Assert.Contains(edit.Modes, option => option.Id == syntheticMode.Id && option.DisplayName == "Test Mode");
        Assert.Equal([syntheticMode.Id], edit.Document.VisibleModesSelection);
    }

    [Fact]
    public async Task StaleEditorCanNotOverwriteNewerRevision()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = CreateService(fixture.Registry);
        var newModel = service.GetNew("Test");
        newModel.Document.Id = "conflict-test";
        newModel.Document.Slug = "conflict-test";
        await service.CreateAsync(newModel.Document);

        var staleEditor = await service.GetEditAsync("Test", "conflict-test");
        var currentEditor = await service.GetEditAsync("Test", "conflict-test");
        Assert.NotNull(staleEditor);
        Assert.NotNull(currentEditor);

        currentEditor.Document.Body += "\n\nCurrent edit.";
        await service.SaveRevisionAsync(currentEditor.Document);

        staleEditor.Document.Body += "\n\nStale edit.";
        await Assert.ThrowsAsync<ContentAuthoringConflictException>(
            () => service.SaveRevisionAsync(staleEditor.Document));
    }

    [Fact]
    public async Task MultipleSlugChangesKeepDirectAliasesToTheStablePage()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = CreateService(fixture.Registry);
        var newModel = service.GetNew("Test");
        newModel.Document.Id = "stable-redirect-page";
        newModel.Document.Slug = "first-slug";
        await service.CreateAsync(newModel.Document);

        var edit = await service.GetEditAsync("Test", "first-slug");
        Assert.NotNull(edit);
        edit.Document.Slug = "second-slug";
        await service.SaveRevisionAsync(edit.Document);

        edit = await service.GetEditAsync("Test", "second-slug");
        Assert.NotNull(edit);
        edit.Document.Slug = "current-slug";
        await service.SaveRevisionAsync(edit.Document);

        foreach (var alias in new[] { "first-slug", "second-slug" })
        {
            var redirect = await fixture.ResolveRedirectAsync(ContentRouteNamespaces.Articles, alias);
            Assert.NotNull(redirect);
            Assert.Equal("stable-redirect-page", redirect.ContentKey);
        }

        Assert.Null(await fixture.ResolveRedirectAsync(ContentRouteNamespaces.Articles, "current-slug"));
    }

    [Fact]
    public async Task NewPageCanNotReuseAnExistingRedirectSlug()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = CreateService(fixture.Registry);
        var firstPage = service.GetNew("Test");
        firstPage.Document.Id = "redirect-owner";
        firstPage.Document.Slug = "reserved-slug";
        await service.CreateAsync(firstPage.Document);

        var edit = await service.GetEditAsync("Test", "reserved-slug");
        Assert.NotNull(edit);
        edit.Document.Slug = "current-owner-slug";
        await service.SaveRevisionAsync(edit.Document);

        var conflictingPage = service.GetNew("Test");
        conflictingPage.Document.Id = "different-page";
        conflictingPage.Document.Slug = "reserved-slug";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(conflictingPage.Document));
        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ContentAuthoringService CreateService(IContentSourceRegistry sourceRegistry) =>
        new(sourceRegistry, new SiteModeRegistry(BuiltInSiteModes.All));

    private sealed class AuthoringSourceFixture : IDisposable
    {
        private readonly string _directory;

        public AuthoringSourceFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-authoring-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:TestDb"] = "Data Source=test-content.db",
                ["ContentStorage:AuthoringSource"] = "Test",
                ["ContentStorage:Sources:Test:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Test:ConnectionString"] = "TestDb",
                ["ContentStorage:GlobalSources:0"] = "Test"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public async Task<ContentItem?> GetBySlugAsync(string slug)
        {
            var options = new DbContextOptionsBuilder<ContentDbContext>();
            Registry.ConfigureDbContext(options, "Test");
            await using var context = new ContentDbContext(options.Options);
            var repository = new DatabaseContentRepository(context);
            return await repository.GetBySlugAsync(slug);
        }

        public Task<ContentRedirectTarget?> ResolveRedirectAsync(string routeNamespace, string slug)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
            {
                ActiveMode = BuiltInSiteModes.Professional
            };
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var redirects = new ContentRedirectService(Registry, accessor);
            return redirects.ResolveAsync(routeNamespace, slug);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // SQLite can briefly hold a file handle on Windows after a context is disposed.
            }
        }
    }
}
