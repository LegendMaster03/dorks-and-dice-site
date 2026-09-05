using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAssetVisibilityTests
{
    [Fact]
    public async Task UnattachedGlobalMediaIsNotServed()
    {
        using var fixture = new VisibilityFixture();
        var asset = await fixture.UploadAsync("Global", "unattached.png", 1);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task AttachedButUnreferencedMediaIsNotServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Global", "attached-page", BuiltInSiteModes.Professional.Id);
        var asset = await fixture.UploadAsync("Global", "attached.png", 2);
        await fixture.Assets.AttachAsync("Global", "attached-page", "Global", asset.AssetKey);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task MediaReferencedByAVisibleCurrentRevisionIsServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Global", "visible-page", BuiltInSiteModes.Professional.Id);
        var asset = await fixture.UploadAsync("Global", "visible.png", 3);
        await fixture.ReferenceAsync("Global", "visible-page", "Global", asset);

        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);

        Assert.NotNull(file);
        Assert.Equal(asset.Sha256, file.Sha256);
    }

    [Fact]
    public async Task MediaReferencedOnlyByASupersededRevisionIsNotServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Global", "revised-page", BuiltInSiteModes.Professional.Id);
        var asset = await fixture.UploadAsync("Global", "superseded.png", 4);
        await fixture.ReferenceAsync("Global", "revised-page", "Global", asset);

        var edit = await fixture.Authoring.GetEditAsync("Global", "revised-page");
        Assert.NotNull(edit);
        edit.Document.Body = "## Current revision\n\nThe image was removed.";
        await fixture.Authoring.SaveRevisionAsync(edit.Document);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task MediaReferencedOnlyByAPageHiddenInTheCurrentModeIsNotServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Global", "hidden-page", BuiltInSiteModes.DorksAndDice.Id);
        var asset = await fixture.UploadAsync("Global", "hidden.png", 5);
        await fixture.ReferenceAsync("Global", "hidden-page", "Global", asset);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task GlobalMediaReferencedByAVisibleModeSourcePageIsServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Mode", "dependent-page", BuiltInSiteModes.Professional.Id);
        var asset = await fixture.UploadAsync("Global", "dependency.png", 6);
        await fixture.ReferenceAsync("Mode", "dependent-page", "Global", asset);

        Assert.NotNull(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task MediaReferencedOnlyByAShadowedPageIsNotServed()
    {
        using var fixture = new VisibilityFixture();
        await fixture.CreatePageAsync("Global", "shadowed-page", BuiltInSiteModes.Professional.Id);
        var asset = await fixture.UploadAsync("Global", "shadowed.png", 7);
        await fixture.ReferenceAsync("Global", "shadowed-page", "Global", asset);
        await fixture.CreatePageAsync("Mode", "shadowed-page", BuiltInSiteModes.Professional.Id);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task MediaUsesStableModeSourceCompositionWithoutLegacyEnumValue()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        using var fixture = new VisibilityFixture(syntheticMode);
        await fixture.CreatePageAsync("Mode", "synthetic-page", syntheticMode.Id);
        var asset = await fixture.UploadAsync("Mode", "synthetic.png", 8);
        await fixture.ReferenceAsync("Mode", "synthetic-page", "Mode", asset);

        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);

        Assert.NotNull(file);
        Assert.Equal(asset.Sha256, file.Sha256);
    }

    private sealed class VisibilityFixture : IDisposable
    {
        private readonly string _directory;

        public VisibilityFixture(SiteModeDefinition? activeMode = null)
        {
            activeMode ??= BuiltInSiteModes.Professional;
            _directory = Path.Combine(Path.GetTempPath(), $"content-visibility-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GlobalDb"] = "Data Source=global.db",
                    ["ConnectionStrings:ModeDb"] = "Data Source=mode.db",
                    ["ContentStorage:AuthoringSource"] = "Mode",
                    ["ContentStorage:Sources:Global:Provider"] = "Sqlite",
                    ["ContentStorage:Sources:Global:ConnectionString"] = "GlobalDb",
                    ["ContentStorage:Sources:Mode:Provider"] = "Sqlite",
                    ["ContentStorage:Sources:Mode:ConnectionString"] = "ModeDb",
                    ["ContentStorage:GlobalSources:0"] = "Global",
                    ["ContentStorage:Modes:professional:Add:0"] = "Mode",
                    ["ContentStorage:Modes:test-mode:Add:0"] = "Mode"
                })
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
            new ContentStorageInitializer(Registry).InitializeAsync().GetAwaiter().GetResult();

            var registeredModes = BuiltInSiteModes.All.ToList();
            if (!registeredModes.Any(mode => string.Equals(mode.Id, activeMode.Id, StringComparison.Ordinal)))
            {
                registeredModes.Add(activeMode);
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
            {
                ActiveMode = activeMode
            };
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var repository = new CompositeContentRepository(accessor, Registry);
            var catalog = new ContentCatalogService(repository);
            Authoring = new ContentAuthoringService(
                Registry,
                new SiteModeRegistry(registeredModes));
            Assets = new ContentAssetService(Registry, accessor, catalog);
        }

        public ContentSourceRegistry Registry { get; }
        public ContentAuthoringService Authoring { get; }
        public ContentAssetService Assets { get; }

        public async Task CreatePageAsync(string sourceKey, string slug, string visibleModeId)
        {
            var model = Authoring.GetNew(sourceKey);
            model.Document.Id = slug;
            model.Document.Slug = slug;
            model.Document.VisibleModesSelection = [visibleModeId];
            await Authoring.CreateAsync(model.Document);
        }

        public async Task<ContentAssetInfo> UploadAsync(string sourceKey, string fileName, byte suffix)
        {
            var data = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, suffix };
            await using var stream = new MemoryStream(data);
            return await Assets.UploadAsync(sourceKey, fileName, "image/png", stream, data.Length);
        }

        public async Task ReferenceAsync(
            string pageSourceKey,
            string slug,
            string assetSourceKey,
            ContentAssetInfo asset)
        {
            await Assets.AttachAsync(pageSourceKey, slug, assetSourceKey, asset.AssetKey);
            var edit = await Authoring.GetEditAsync(pageSourceKey, slug);
            Assert.NotNull(edit);
            edit.Document.Body = $"![Referenced image]({asset.Url})";
            await Authoring.SaveRevisionAsync(edit.Document);
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
