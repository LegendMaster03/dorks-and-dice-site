using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
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
            "diagram.png",
            "image/png",
            imageStream,
            pngSignature.Length);
        await assets.AttachAsync("Source", "copy-history-test", "Source", sourceAsset.AssetKey);

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
            "second-diagram.png",
            "image/png",
            secondImageStream,
            secondPng.Length);
        await assets.AttachAsync("Source", "copy-history-test", "Source", secondSourceAsset.AssetKey);

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
    public async Task CopyAllSynchronizesCanonicalSlugChangesForTheSameStableIdentity()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var sourceModel = authoring.GetNew("Source");
        sourceModel.Document.Id = "stable-page";
        sourceModel.Document.Slug = "original-slug";
        await authoring.CreateAsync(sourceModel.Document);
        Assert.Equal(1, await transfer.CopyAllAsync("Source", "Target"));

        var sourceEdit = await authoring.GetEditAsync("Source", "original-slug");
        Assert.NotNull(sourceEdit);
        sourceEdit.Document.Slug = "current-slug";
        await authoring.SaveRevisionAsync(sourceEdit.Document);

        Assert.Equal(1, await transfer.CopyAllAsync("Source", "Target"));

        Assert.Null(await authoring.GetEditAsync("Target", "original-slug"));
        var synchronizedTarget = await authoring.GetEditAsync("Target", "current-slug");
        Assert.NotNull(synchronizedTarget);
        Assert.Equal("stable-page", synchronizedTarget.Document.Id);

        var redirect = fixture.CreateRedirectService("Target");
        var target = await redirect.ResolveAsync(ContentRouteNamespaces.Articles, "original-slug");
        Assert.NotNull(target);
        Assert.Equal("stable-page", target.ContentKey);
    }

    [Fact]
    public async Task CopyAllRejectsADifferentStableIdentityReusingTheCanonicalSlug()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var sourceModel = authoring.GetNew("Source");
        sourceModel.Document.Id = "source-page";
        sourceModel.Document.Slug = "shared-slug";
        await authoring.CreateAsync(sourceModel.Document);

        var targetModel = authoring.GetNew("Target");
        targetModel.Document.Id = "different-target-page";
        targetModel.Document.Slug = "shared-slug";
        await authoring.CreateAsync(targetModel.Document);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transfer.CopyAllAsync("Source", "Target"));

        Assert.NotNull(await authoring.GetEditAsync("Source", "shared-slug"));
        Assert.NotNull(await authoring.GetEditAsync("Target", "shared-slug"));
    }

    [Fact]
    public async Task CopyAllPreservesRedirectAliasesForStablePages()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var transfer = new ContentSourceTransferService(fixture.Registry);

        var model = authoring.GetNew("Source");
        model.Document.Id = "redirect-transfer-test";
        model.Document.Slug = "original-article-slug";
        await authoring.CreateAsync(model.Document);

        var edit = await authoring.GetEditAsync("Source", "original-article-slug");
        Assert.NotNull(edit);
        edit.Document.Slug = "current-article-slug";
        await authoring.SaveRevisionAsync(edit.Document);

        Assert.Equal(1, await transfer.CopyAllAsync("Source", "Target"));

        var redirects = fixture.CreateRedirectService("Target");
        var target = await redirects.ResolveAsync(
            ContentRouteNamespaces.Articles,
            "original-article-slug");

        Assert.NotNull(target);
        Assert.Equal("redirect-transfer-test", target.ContentKey);
    }

    [Fact]
    public async Task MovePromotesTheCompletePageAndOwnedMediaThenRemovesTheAuthoringCopy()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);

        var model = authoring.GetNew("Source");
        model.Document.Id = "promote-test";
        model.Document.Slug = "promote-test";
        await authoring.CreateAsync(model.Document);

        var edit = await authoring.GetEditAsync("Source", "promote-test");
        Assert.NotNull(edit);
        edit.Document.Body += "\n\nReady for review.";
        await authoring.SaveRevisionAsync(edit.Document);

        var png = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        await using var stream = new MemoryStream(png);
        var asset = await assets.UploadAsync(
            "Source", "unused-but-attached.png", "image/png", stream, png.Length);
        await assets.AttachAsync("Source", "promote-test", "Source", asset.AssetKey);

        await authoring.MoveAsync("Source", "Target", "promote-test");

        Assert.Null(await authoring.GetEditAsync("Source", "promote-test"));
        var promoted = await authoring.GetEditAsync("Target", "promote-test");
        Assert.NotNull(promoted);
        Assert.Equal(2, promoted.History.Count);
        Assert.Contains("Ready for review.", promoted.Document.Body);
        var promotedAsset = Assert.Single(await assets.GetForPageAsync("Target", "promote-test"));
        Assert.Equal(asset.AssetKey, promotedAsset.AssetKey);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => assets.GetForPageAsync("Source", "promote-test"));
    }

    [Fact]
    public async Task ArticleMayReferenceAttachedOwnOrGlobalMediaButRejectsUnattachedMedia()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);
        var model = authoring.GetNew("Source");
        model.Document.Id = "dependency-test";
        model.Document.Slug = "dependency-test";
        await authoring.CreateAsync(model.Document);

        var png = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        await using var globalStream = new MemoryStream(png);
        var global = await assets.UploadAsync("Target", "global.png", "image/png", globalStream, png.Length);
        await assets.AttachAsync("Source", "dependency-test", "Target", global.AssetKey);

        var edit = await authoring.GetEditAsync("Source", "dependency-test");
        Assert.NotNull(edit);
        edit.Document.Body = $"![Global]({global.Url})";
        await authoring.SaveRevisionAsync(edit.Document);

        await using var localStream = new MemoryStream(png.Concat(new byte[] { 1 }).ToArray());
        var unattached = await assets.UploadAsync("Source", "unattached.png", "image/png", localStream, png.Length + 1);
        edit = await authoring.GetEditAsync("Source", "dependency-test");
        Assert.NotNull(edit);
        edit.Document.Body += $"\n\n![Missing]({unattached.Url})";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => authoring.SaveRevisionAsync(edit.Document));
        Assert.Contains(unattached.AssetKey, error.Message);
    }

    [Fact]
    public async Task PushAllPromotesEveryPageAndClearsTheAuthoringWorkspace()
    {
        using var fixture = new TransferFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        foreach (var slug in new[] { "bulk-one", "bulk-two" })
        {
            var model = authoring.GetNew("Source");
            model.Document.Id = slug;
            model.Document.Slug = slug;
            await authoring.CreateAsync(model.Document);
        }

        Assert.Equal(2, await authoring.MoveAllAsync("Source", "Target"));

        Assert.Null(await authoring.GetEditAsync("Source", "bulk-one"));
        Assert.Null(await authoring.GetEditAsync("Source", "bulk-two"));
        Assert.NotNull(await authoring.GetEditAsync("Target", "bulk-one"));
        Assert.NotNull(await authoring.GetEditAsync("Target", "bulk-two"));
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
                ["ContentStorage:Sources:Target:ConnectionString"] = "TargetDb",
                ["ContentStorage:GlobalSources:0"] = "Target"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public ContentRedirectService CreateRedirectService(string sourceKey)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
            {
                SiteMode = SiteMode.Professional,
                IsDevelopmentPreview = true,
                HasContentSourceOverride = true,
                EnabledContentSources = new HashSet<string>([sourceKey], StringComparer.OrdinalIgnoreCase)
            };
            return new ContentRedirectService(
                Registry,
                new HttpContextAccessor { HttpContext = httpContext });
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
