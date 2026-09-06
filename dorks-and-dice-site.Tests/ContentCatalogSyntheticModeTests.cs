using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Tests;

public sealed class ContentCatalogSyntheticModeTests
{
    [Fact]
    public async Task DeveloperInSyntheticDevelopmentListsContentAcrossNormalModeAssignments()
    {
        var dorks = Article("dorks", BuiltInSiteModes.DorksAndDice.Id);
        var professional = Article("professional", BuiltInSiteModes.Professional.Id);
        var catalog = new ContentCatalogService(new StubContentRepository([dorks, professional]));
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true,
            IsDevelopmentPreview = true
        };

        var items = await catalog.GetByContextAsync(ContentTags.Article, context);

        Assert.Equal(["dorks", "professional"], items.Select(item => item.Id));
        Assert.Same(dorks, await catalog.GetForDetailAsync("dorks", context));
    }

    [Fact]
    public async Task NonDeveloperTrustedPreviewRemainsBoundToSelectedMode()
    {
        var dorks = Article("dorks", BuiltInSiteModes.DorksAndDice.Id);
        var professional = Article("professional", BuiltInSiteModes.Professional.Id);
        var catalog = new ContentCatalogService(new StubContentRepository([dorks, professional]));
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            FrameworkState = SyntheticSiteModes.Development,
            HasTrustedAccess = true
        };

        var items = await catalog.GetByContextAsync(ContentTags.Article, context);

        Assert.Equal(["professional"], items.Select(item => item.Id));
        Assert.Null(await catalog.GetForDetailAsync("dorks", context));
    }

    [Fact]
    public async Task NormalModeStillFiltersContentByModeAssignment()
    {
        var dorks = Article("dorks", BuiltInSiteModes.DorksAndDice.Id);
        var professional = Article("professional", BuiltInSiteModes.Professional.Id);
        var catalog = new ContentCatalogService(new StubContentRepository([dorks, professional]));
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional
        };

        var items = await catalog.GetByContextAsync(ContentTags.Article, context);

        Assert.Equal(["professional"], items.Select(item => item.Id));
        Assert.Null(await catalog.GetForDetailAsync("dorks", context));
    }

    private static ContentItem Article(string id, string modeId) => new()
    {
        Id = id,
        Slug = id,
        Title = id,
        Summary = id,
        Tags = [ContentTags.Article],
        VisibleInModes = [modeId]
    };

    private sealed class StubContentRepository : IContentRepository
    {
        private readonly IReadOnlyList<ContentItem> _items;

        public StubContentRepository(IReadOnlyList<ContentItem> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_items);

        public Task<ContentItem?> GetBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.SingleOrDefault(item =>
                string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase)));
    }
}
