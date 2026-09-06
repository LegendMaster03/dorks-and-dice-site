using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAssetReplacementTests
{
    [Fact]
    public async Task ReplacementPreservesStableIdentityAndPageDependency()
    {
        using var fixture = new ReplacementFixture();
        var authoring = new ContentAuthoringService(fixture.Registry);
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);

        var page = authoring.GetNew("Source");
        page.Document.Id = "replacement-page";
        page.Document.Slug = "replacement-page";
        await authoring.CreateAsync(page.Document);

        var originalBytes = PngBytes(1);
        await using var originalStream = new MemoryStream(originalBytes);
        var original = await assets.UploadAsync(
            "Source", "resume-preview.png", "image/png", originalStream, originalBytes.Length);
        await assets.AttachAsync("Source", "replacement-page", "Source", original.AssetKey);

        var replacementBytes = PngBytes(2, 3, 4);
        await using var replacementStream = new MemoryStream(replacementBytes);
        var replaced = await ContentAssetReplacement.ReplaceAsync(
            assets,
            fixture.Registry,
            "Source",
            original.AssetKey,
            "renamed-by-upload.png",
            "image/png",
            replacementStream,
            replacementBytes.Length);

        Assert.Equal(original.AssetKey, replaced.AssetKey);
        Assert.Equal(original.FileName, replaced.FileName);
        Assert.Equal(original.Url, replaced.Url);
        Assert.NotEqual(original.Sha256, replaced.Sha256);
        Assert.Equal(replacementBytes.Length, replaced.Length);

        var pageAssets = await assets.GetForPageAsync("Source", "replacement-page");
        var pageAsset = Assert.Single(pageAssets);
        Assert.Equal(original.AssetKey, pageAsset.AssetKey);

        var file = await assets.GetFromSourceAsync("Source", original.AssetKey);
        Assert.NotNull(file);
        Assert.Equal(replacementBytes, file.Data);
        Assert.Single(await assets.GetForSourceAsync("Source"));
    }

    [Fact]
    public async Task IdenticalReplacementIsANoOp()
    {
        using var fixture = new ReplacementFixture();
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);
        var bytes = PngBytes(7);
        await using var originalStream = new MemoryStream(bytes);
        var original = await assets.UploadAsync("Source", "same.png", "image/png", originalStream, bytes.Length);

        await using var replacementStream = new MemoryStream(bytes);
        var replaced = await ContentAssetReplacement.ReplaceAsync(
            assets,
            fixture.Registry,
            "Source",
            original.AssetKey,
            "different-name.png",
            "image/png",
            replacementStream,
            bytes.Length);

        Assert.Equal(original.AssetKey, replaced.AssetKey);
        Assert.Equal(original.FileName, replaced.FileName);
        Assert.Equal(original.Sha256, replaced.Sha256);
        Assert.Single(await assets.GetForSourceAsync("Source"));
    }

    [Fact]
    public async Task ReplacementRejectsMediaTypeChangeWithoutLeavingStagingAsset()
    {
        using var fixture = new ReplacementFixture();
        var assets = new ContentAssetService(fixture.Registry, new HttpContextAccessor(), null!);
        var png = PngBytes(9);
        await using var originalStream = new MemoryStream(png);
        var original = await assets.UploadAsync("Source", "original.png", "image/png", originalStream, png.Length);

        var pdf = "%PDF-1.7\nreplacement"u8.ToArray();
        await using var replacementStream = new MemoryStream(pdf);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ContentAssetReplacement.ReplaceAsync(
                assets,
                fixture.Registry,
                "Source",
                original.AssetKey,
                "replacement.pdf",
                "application/pdf",
                replacementStream,
                pdf.Length));

        Assert.Contains("must keep the existing media type", error.Message);
        var remaining = Assert.Single(await assets.GetForSourceAsync("Source"));
        Assert.Equal(original.AssetKey, remaining.AssetKey);
        Assert.Equal(original.Sha256, remaining.Sha256);
    }

    private static byte[] PngBytes(params byte[] payload) =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, .. payload];

    private sealed class ReplacementFixture : IDisposable
    {
        private readonly string _directory;

        public ReplacementFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-asset-replacement-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SourceDb"] = "Data Source=source.db",
                ["ContentStorage:AuthoringSource"] = "Source",
                ["ContentStorage:Sources:Source:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Source:ConnectionString"] = "SourceDb"
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
                // SQLite can briefly hold a file handle on Windows after disposal.
            }
        }
    }
}
