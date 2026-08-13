using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringServiceTests
{
    [Fact]
    public async Task SavingCreatesRevisionAndPreservesStablePageIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ContentDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repository = new DatabaseContentRepository(context);
        var service = new ContentAuthoringService(context);
        var newModel = service.GetNew();
        newModel.Document.Id = "authoring-test";
        newModel.Document.Slug = "authoring-test";

        var created = await service.CreateAsync(newModel.Document);
        var editModel = await service.GetEditAsync(created.Slug);

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

        var reloaded = await service.GetEditAsync("authoring-test-moved");
        Assert.NotNull(reloaded);
        Assert.Equal(saved.RevisionId, reloaded.Document.ExpectedRevisionId);
        Assert.Equal(2, reloaded.History.Count);
        Assert.Equal(firstRevisionId, reloaded.History[0].ParentRevisionId);
        Assert.Contains("Second revision.", reloaded.Document.Body);
        Assert.Null(await repository.GetBySlugAsync("authoring-test"));
    }

    [Fact]
    public async Task StaleEditorCanNotOverwriteNewerRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ContentDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new ContentAuthoringService(context);
        var newModel = service.GetNew();
        newModel.Document.Id = "conflict-test";
        newModel.Document.Slug = "conflict-test";
        await service.CreateAsync(newModel.Document);

        var staleEditor = await service.GetEditAsync("conflict-test");
        var currentEditor = await service.GetEditAsync("conflict-test");
        Assert.NotNull(staleEditor);
        Assert.NotNull(currentEditor);

        currentEditor.Document.Body += "\n\nCurrent edit.";
        await service.SaveRevisionAsync(currentEditor.Document);

        staleEditor.Document.Body += "\n\nStale edit.";
        await Assert.ThrowsAsync<ContentAuthoringConflictException>(
            () => service.SaveRevisionAsync(staleEditor.Document));
    }
}
