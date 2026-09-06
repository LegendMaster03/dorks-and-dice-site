using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class HomepageContentServiceTests
{
    [Fact]
    public async Task NormalModeHomepageUsesHomepageContextAndComposesBody()
    {
        var item = new ContentItem
        {
            Id = "home",
            Slug = "home",
            Title = "Home",
            BodyFormat = "markdown",
            Body = "# Hello",
            VisibleInModes = ["test-mode"]
        };
        var catalog = new TestContentCatalog([item]);
        var composer = new TestPageComposer();
        var service = new HomepageContentService(catalog, composer);
        var context = new SiteModeContext
        {
            ActiveMode = new SiteModeDefinition(
                "test-mode",
                "Test Mode",
                null,
                "TestMode",
                "test-mode")
        };

        var result = await service.GetAsync(context);

        Assert.NotNull(result);
        Assert.Same(item, result.Item);
        var fragment = Assert.Single(result.Fragments);
        Assert.Equal("composed:markdown:# Hello", fragment.RenderedHtml);
        Assert.Equal(ContentTags.Homepage, catalog.LastContextTag);
        Assert.True(catalog.LastIncludeUnlisted);
    }

    [Fact]
    public async Task FrameworkStateWithoutNormalModeHasNoHomepageContent()
    {
        var catalog = new TestContentCatalog(
        [
            new ContentItem { Id = "home", Slug = "home", Title = "Home" }
        ]);
        var service = new HomepageContentService(catalog, new TestPageComposer());

        var result = await service.GetAsync(new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.Fallback
        });

        Assert.Null(result);
        Assert.Null(catalog.LastContextTag);
    }

    [Fact]
    public async Task MultipleVisibleHomepageDocumentsSelectMostSpecificNewestRevision()
    {
        var shared = new ContentItem
        {
            Id = "shared-home",
            Slug = "shared-home",
            Title = "Shared Home",
            RevisionId = 30,
            VisibleInModes = [BuiltInSiteModes.Professional.Id, BuiltInSiteModes.DorksAndDice.Id]
        };
        var olderDedicated = new ContentItem
        {
            Id = "dorks-home-old",
            Slug = "dorks-home-old",
            Title = "Dorks Home Old",
            RevisionId = 10,
            VisibleInModes = [BuiltInSiteModes.DorksAndDice.Id]
        };
        var newestDedicated = new ContentItem
        {
            Id = "dorks-home-new",
            Slug = "dorks-home-new",
            Title = "Dorks Home New",
            RevisionId = 20,
            VisibleInModes = [BuiltInSiteModes.DorksAndDice.Id]
        };
        var catalog = new TestContentCatalog([shared, olderDedicated, newestDedicated]);
        var service = new HomepageContentService(catalog, new TestPageComposer());
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.DorksAndDice
        };

        var result = await service.GetAsync(context);

        Assert.NotNull(result);
        Assert.Same(newestDedicated, result.Item);
    }

    private sealed class TestPageComposer : IContentPageComposer
    {
        public IReadOnlyList<ContentPageFragment> Compose(string format, string body) =>
            [ContentPageFragment.Html($"composed:{format}:{body}")];
    }

    private sealed class TestContentCatalog(IReadOnlyList<ContentItem> items) : IContentCatalogService
    {
        public string? LastContextTag { get; private set; }
        public bool LastIncludeUnlisted { get; private set; }

        public Task<IReadOnlyList<ContentItem>> GetByContextAsync(
            string contextTag,
            SiteModeContext modeContext,
            bool includeUnlisted = false,
            CancellationToken cancellationToken = default)
        {
            LastContextTag = contextTag;
            LastIncludeUnlisted = includeUnlisted;
            return Task.FromResult(items);
        }

        public Task<ContentItem?> GetForDetailAsync(
            string slug,
            SiteModeContext modeContext,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContentItem?> GetForDetailByIdAsync(
            string contentKey,
            SiteModeContext modeContext,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ContentItem>> GetByContextAsync(
            string contextTag,
            SiteMode siteMode,
            bool includeUnlisted = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContentItem?> GetForDetailAsync(
            string slug,
            SiteMode siteMode,
            bool isDevelopmentPreview,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContentItem?> GetForDetailByIdAsync(
            string contentKey,
            SiteMode siteMode,
            bool isDevelopmentPreview,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
