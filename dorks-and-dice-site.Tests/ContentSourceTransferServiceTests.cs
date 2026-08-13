using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSourceTransferServiceTests
{
    [Fact]
    public async Task CopyAllPreservesSourceAndRevisionHistory()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var model = authoring.GetNew("Source");
        model.Document.Id = "copy-history-test";
        model.Document.Slug = "copy-history-test";
        await authoring.CreateAsync(model.Document);

        var edit = await authoring.GetEditAsync("Source", "copy-history-test");
        Assert.NotNull(edit);
        edit.Document.Body += "\n\nSecond revision.";
        await authoring.SaveRevisionAsync(edit.Document);

        var copiedCount = await transfer.CopyAllAsync("Source", "Target");

        Assert.Equal(1, copiedCount);

        var sourceCopy = await authoring.GetEditAsync("Source", "copy-history-test");
        var targetCopy = await authoring.GetEditAsync("Target", "copy-history-test");
        Assert.NotNull(sourceCopy);
        Assert.NotNull(targetCopy);
        Assert.Equal(2, sourceCopy.History.Count);
        Assert.Equal(2, targetCopy.History.Count);
        Assert.Contains("Second revision.", targetCopy.Document.Body);
        Assert.Equal(sourceCopy.Document.Id, targetCopy.Document.Id);
        Assert.Equal(sourceCopy.Document.Slug, targetCopy.Document.Slug);
    }

    [Fact]
    public async Task CopyAllRejectsConflictingTargetWithoutRemovingSource()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var sourceModel = authoring.GetNew("Source");
        sourceModel.Document.Id = "conflict-test";
        sourceModel.Document.Slug = "conflict-test";
        await authoring.CreateAsync(sourceModel.Document);

        var targetModel = authoring.GetNew("Target");
        targetModel.Document.Id = "conflict-test";
        targetModel.Document.Slug = "conflict-test";
        await authoring.CreateAsync(targetModel.Document);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transfer.CopyAllAsync("Source", "Target"));

        Assert.NotNull(await authoring.GetEditAsync("Source", "conflict-test"));
    }

    private sealed class TransferFixture : IDisposable
    {
        private readonly string _directory;

        public TransferFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-transfer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SourceDb"] = "Data Source=source.db",
                ["ConnectionStrings:TargetDb"] = "Data Source=target.db",
                ["ContentStorage:AuthoringSource"] = "Source",
                ["ContentStorage:Sources:Source:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Source:ConnectionString"] = "SourceDb",
                ["ContentStorage:Sources:Target:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Target:ConnectionString"] = "TargetDb"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

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
