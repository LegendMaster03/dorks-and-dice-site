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
    public async Task SharedPdfRequiresDependenciesAndCurrentReferencesAndPreservesBytes()
    {
        using var fixture = new VisibilityFixture();
        var bytes = "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\n%%EOF"u8.ToArray();
        using var stream = new MemoryStream(bytes);
        var asset = await fixture.Assets.UploadAsync("Global", "Authored document.pdf", "application/pdf", stream, bytes.Length);
        Assert.Equal("Authored-document.pdf", asset.FileName);
        Assert.Equal($"[{asset.FileName}]({asset.Url})", asset.MarkdownReference);
        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
        using var duplicate = new MemoryStream(bytes);
        Assert.Equal(asset.AssetKey, (await fixture.Assets.UploadAsync("Global", "copy.pdf", "application/pdf", duplicate, bytes.Length)).AssetKey);
        foreach (var slug in new[] { "pdf-first", "pdf-second" })
        {
            await fixture.CreatePageAsync("Global", slug, BuiltInSiteModes.Professional.Id);
            var edit = (await fixture.Authoring.GetEditAsync("Global", slug))!;
            edit.Document.Body = $"[Read document]({asset.Url})";
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Authoring.SaveRevisionAsync(edit.Document));
            await fixture.Assets.AttachAsync("Global", slug, "Global", asset.AssetKey);
            await fixture.Assets.AttachAsync("Global", slug, "Global", asset.AssetKey);
            await fixture.Authoring.SaveRevisionAsync(edit.Document);
            Assert.Single(await fixture.Assets.GetForPageAsync("Global", slug));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Assets.DetachAsync("Global", slug, asset.AssetKey));
        }
        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);
        Assert.NotNull(file);
        Assert.Equal("application/pdf", file.MediaType);
        Assert.Equal(bytes, file.Data);
        foreach (var slug in new[] { "pdf-first", "pdf-second" })
        {
            var edit = (await fixture.Authoring.GetEditAsync("Global", slug))!;
            edit.Document.Body = "Document removed.";
            await fixture.Authoring.SaveRevisionAsync(edit.Document);
            await fixture.Assets.DetachAsync("Global", slug, asset.AssetKey);
            if (slug == "pdf-first") Assert.NotNull(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
        }
        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    [Fact]
    public async Task PdfUploadRejectsAnIncorrectSignature()
    {
        using var fixture = new VisibilityFixture();
        using var stream = new MemoryStream("<html>not a PDF</html>"u8.ToArray());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Assets.UploadAsync(
            "Global", "document.pdf", "application/pdf", stream, stream.Length));
        Assert.Empty(await fixture.Assets.GetForSourceAsync("Global"));
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><foreignObject/></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><use href='https://example.com/a.svg'/></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><style>@import 'https://example.com/style';</style></svg>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///secret'>]><svg xmlns='http://www.w3.org/2000/svg'>&x;</svg>")]
    [InlineData("not XML")]
    public async Task SvgRejectsActiveOrMalformedDocuments(string svg)
    {
        using var fixture = new VisibilityFixture();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Assets.UploadAsync(
            "Global", "logo.svg", "image/svg+xml", stream, stream.Length));
        Assert.Empty(await fixture.Assets.GetForSourceAsync("Global"));
    }

    [Fact]
    public async Task PassiveSvgInMetadataCreatesAVisibleRevisionDependency()
    {
        using var fixture = new VisibilityFixture();
        var bytes = "<svg xmlns='http://www.w3.org/2000/svg' role='img' viewBox='0 0 10 10'><path d='M0 0L10 10'/></svg>"u8.ToArray();
        using var stream = new MemoryStream(bytes);
        var asset = await fixture.Assets.UploadAsync("Global", "logo.svg", "image/svg+xml", stream, bytes.Length);
        await fixture.CreatePageAsync("Global", "svg-page", BuiltInSiteModes.Professional.Id);
        var edit = (await fixture.Authoring.GetEditAsync("Global", "svg-page"))!;
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(edit.Document.MetadataJson)!;
        metadata["header"]!["logoUrl"] = asset.Url;
        edit.Document.MetadataJson = metadata.ToJsonString();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Authoring.SaveRevisionAsync(edit.Document));
        await fixture.Assets.AttachAsync("Global", "svg-page", "Global", asset.AssetKey);
        await fixture.Authoring.SaveRevisionAsync(edit.Document);
        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);
        Assert.NotNull(file);
        Assert.Equal(bytes, file.Data);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Assets.DetachAsync("Global", "svg-page", asset.AssetKey));
    }

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
        var syntheticNormalMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        using var fixture = new VisibilityFixture(activeMode: syntheticNormalMode);
        await fixture.CreatePageAsync("Mode", "synthetic-page", syntheticNormalMode.Id);
        var asset = await fixture.UploadAsync("Mode", "synthetic.png", 8);
        await fixture.ReferenceAsync("Mode", "synthetic-page", "Mode", asset);

        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);

        Assert.NotNull(file);
        Assert.Equal(asset.Sha256, file.Sha256);
    }

    [Fact]
    public async Task SyntheticDevelopmentServesSelectedDatabaseMediaAcrossModeAssignments()
    {
        var modeContext = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(["Mode"], StringComparer.OrdinalIgnoreCase)
        };
        using var fixture = new VisibilityFixture(modeContext: modeContext);
        await fixture.CreatePageAsync("Mode", "development-page", BuiltInSiteModes.DorksAndDice.Id);
        var asset = await fixture.UploadAsync("Mode", "development.png", 9);
        await fixture.ReferenceAsync("Mode", "development-page", "Mode", asset);

        var file = await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName);

        Assert.NotNull(file);
        Assert.Equal(asset.Sha256, file.Sha256);
    }

    [Fact]
    public async Task SyntheticDevelopmentWithNoSelectedDatabaseDoesNotFallBackToAuthoringMedia()
    {
        var modeContext = new SiteModeContext
        {
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        using var fixture = new VisibilityFixture(modeContext: modeContext);
        await fixture.CreatePageAsync("Mode", "unselected-page", BuiltInSiteModes.DorksAndDice.Id);
        var asset = await fixture.UploadAsync("Mode", "unselected.png", 10);
        await fixture.ReferenceAsync("Mode", "unselected-page", "Mode", asset);

        Assert.Null(await fixture.Assets.GetForRequestAsync(asset.AssetKey, asset.FileName));
    }

    private sealed class VisibilityFixture : IDisposable
    {
        private readonly string _directory;

        public VisibilityFixture(
            SiteModeDefinition? activeMode = null,
            SiteModeContext? modeContext = null)
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

            modeContext ??= new SiteModeContext
            {
                ActiveMode = activeMode
            };
            var httpContext = new DefaultHttpContext();
            httpContext.Items[SiteModeContext.HttpContextItemKey] = modeContext;
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
