using dorks_and_dice_site.Framework.Fallback;
using dorks_and_dice_site.Framework.TrustedPreview;
using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModePresentationServiceTests
{
    [Fact]
    public void SyntheticModeCanResolvePresentationByStableId()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var service = CreateService(
            new FallbackPresentationModule(),
            new SyntheticPresentationModule("test-mode", "Synthetic Site"));

        var title = service.GetTitleSuffix(new SiteModeContext
        {
            ActiveMode = syntheticMode
        });

        Assert.Equal("Synthetic Site", title);
    }

    [Fact]
    public void MissingModePresentationFallsBackToFrameworkPresentation()
    {
        var service = CreateService(new FallbackPresentationModule());

        var title = service.GetTitleSuffix(new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        });

        Assert.Equal("Unassigned Site", title);
    }

    [Fact]
    public void TrustedPreviewOverlaysSelectedModeInsteadOfReplacingItsPresentation()
    {
        var syntheticMode = new SiteModeDefinition(
            Id: "test-mode",
            DisplayName: "Test Mode",
            LegacyMode: null,
            ViewFolder: "TestMode",
            AssetFolder: "test-mode");
        var service = CreateService(
            new FallbackPresentationModule(),
            new TrustedPreviewPresentationModule(),
            new SyntheticPresentationModule("test-mode", "Synthetic Site"));

        var selectedModeTitle = service.GetTitleSuffix(new SiteModeContext
        {
            ActiveMode = syntheticMode,
            FrameworkState = FrameworkRuntimeStates.TrustedPreview
        });
        var previewOnlyTitle = service.GetTitleSuffix(new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.TrustedPreview
        });

        Assert.Equal("Synthetic Site", selectedModeTitle);
        Assert.Equal("Development Preview", previewOnlyTitle);
    }

    [Fact]
    public void DuplicatePresentationKeysAreRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateService(
            new FallbackPresentationModule(),
            new SyntheticPresentationModule("test-mode", "First"),
            new SyntheticPresentationModule("test-mode", "Second")));

        Assert.Contains("Duplicate presentation key", exception.Message, StringComparison.Ordinal);
    }

    private static SiteModePresentationService CreateService(
        params ISiteModePresentationModule[] modules) =>
        new(modules, new TestWebHostEnvironment());

    private sealed class SyntheticPresentationModule : ISiteModePresentationModule
    {
        private readonly string _title;

        public SyntheticPresentationModule(string presentationKey, string title)
        {
            PresentationKey = presentationKey;
            _title = title;
        }

        public string PresentationKey { get; }
        public string GetTitleSuffix() => _title;
        public string GetDefaultMetaDescription() => "Synthetic description";
        public string GetFaviconPath() => "https://example.test/favicon.svg";
        public string? GetDefaultMetaImagePath() => null;
        public string? GetStructuredDataJson(string canonicalOrigin) => null;
        public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation() => new()
        {
            MetaTitle = $"Articles - {_title}",
            MetaDescription = "Synthetic description",
            Eyebrow = "Articles",
            Title = "Articles",
            Description = "Synthetic description",
            EmptyStateText = "No articles."
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "dorks-and-dice-site.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
