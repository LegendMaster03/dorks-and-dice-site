using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringServiceTests
{
    [Fact]
    public async Task SavingCreatesRevisionAndPreservesStablePageIdentity()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = new ContentAuthoringService(fixture.Registry);
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
    }

    [Fact]
    public async Task StaleEditorCanNotOverwriteNewerRevision()
    {
        using var fixture = new AuthoringSourceFixture();
        var service = new ContentAuthoringService(fixture.Registry);
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
                ["ContentStorage:Sources:Test:ConnectionString"] = "TestDb"
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
