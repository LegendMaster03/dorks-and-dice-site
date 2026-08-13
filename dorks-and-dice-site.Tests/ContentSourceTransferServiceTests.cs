using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSourceTransferServiceTests
{
    [Fact]
    public async Task CopyAllPreservesSourceRevisionHistoryAndMediaAcrossRepeatedSynchronization()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);

        var model = authoring.GetNew("Source");
        model.Document.Id = "copy-history-test";
        model.Document.Slug = "copy-history-test";
        await authoring.CreateAsync(model.Document);

        var edit = await authoring.GetEditAsync("Source", "copy-history-test");
        Assert.NotNull(edit);
        edit.Document.Body += "\n\nSecond revision.";
        await authoring.SaveRevisionAsync(edit.Document);

        var pngSignature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        await using var imageStream = new MemoryStream(pngSignature);
        var sourceAsset = await assets.UploadAsync(
            "Source",
            "copy-history-test",
            "diagram.png",
            "image/png",
            imageStream,
            pngSignature.Length);

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

        var targetAssets = await assets.GetForPageAsync("Target", "copy-history-test");
        var targetAsset = Assert.Single(targetAssets);
        Assert.Equal(sourceAsset.AssetKey, targetAsset.AssetKey);
        Assert.Equal(sourceAsset.Sha256, targetAsset.Sha256);
        Assert.Equal(sourceAsset.Url, targetAsset.Url);

        var latestEdit = await authoring.GetEditAsync("Source", "copy-history-test");
        Assert.NotNull(latestEdit);
        latestEdit.Document.Body += "\n\nThird revision.";
        await authoring.SaveRevisionAsync(latestEdit.Document);

        var secondPng = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01 };
        await using var secondImageStream = new MemoryStream(secondPng);
        var secondSourceAsset = await assets.UploadAsync(
            "Source",
            "copy-history-test",
            "second-diagram.png",
            "image/png",
            secondImageStream,
            secondPng.Length);

        Assert.Equal(1, await transfer.CopyAllAsync("Source", "Target"));

        targetCopy = await authoring.GetEditAsync("Target", "copy-history-test");
        Assert.NotNull(targetCopy);
        Assert.Equal(3, targetCopy.History.Count);
        Assert.Contains("Third revision.", targetCopy.Document.Body);
        targetAssets = await assets.GetForPageAsync("Target", "copy-history-test");
        Assert.Equal(2, targetAssets.Count);
        Assert.Contains(targetAssets, asset => asset.AssetKey == sourceAsset.AssetKey);
        Assert.Contains(targetAssets, asset => asset.AssetKey == secondSourceAsset.AssetKey);
    }

    [Fact]
    public async Task CopyAllReplacesTheSameStablePageInTheTarget()
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

        var targetEdit = await authoring.GetEditAsync("Target", "conflict-test");
        Assert.NotNull(targetEdit);
        targetEdit.Document.Body = "Target-only body.";
        await authoring.SaveRevisionAsync(targetEdit.Document);

        Assert.Equal(1, await transfer.CopyAllAsync("Source", "Target"));

        Assert.NotNull(await authoring.GetEditAsync("Source", "conflict-test"));
        var synchronizedTarget = await authoring.GetEditAsync("Target", "conflict-test");
        Assert.NotNull(synchronizedTarget);
        Assert.DoesNotContain("Target-only body.", synchronizedTarget.Document.Body);
        Assert.Single(synchronizedTarget.History);
    }

    [Fact]
    public async Task CopyAllRejectsAStableIdentityConflictWithoutChangingEitherSource()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var sourceModel = authoring.GetNew("Source");
        sourceModel.Document.Id = "identity-conflict";
        sourceModel.Document.Slug = "source-slug";
        await authoring.CreateAsync(sourceModel.Document);

        var targetModel = authoring.GetNew("Target");
        targetModel.Document.Id = "identity-conflict";
        targetModel.Document.Slug = "different-target-slug";
        await authoring.CreateAsync(targetModel.Document);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transfer.CopyAllAsync("Source", "Target"));

        Assert.NotNull(await authoring.GetEditAsync("Source", "source-slug"));
        Assert.NotNull(await authoring.GetEditAsync("Target", "different-target-slug"));
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
