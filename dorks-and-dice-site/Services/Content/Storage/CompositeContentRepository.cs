using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public sealed class CompositeContentRepository : IContentRepository
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentSourceRegistry _sourceRegistry;

    public CompositeContentRepository(
        IHttpContextAccessor httpContextAccessor,
        IContentSourceRegistry sourceRegistry)
    {
        _httpContextAccessor = httpContextAccessor;
        _sourceRegistry = sourceRegistry;
    }

    public async Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var siteModeContext = GetSiteModeContext();
        var sources = siteModeContext.IsDevelopmentPreview && siteModeContext.HasContentSourceOverride
            ? _sourceRegistry.GetSourcesByKeys(siteModeContext.EnabledContentSources)
            : _sourceRegistry.GetDefaultSources(siteModeContext.SiteMode);

        var itemsById = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);
        var slugOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var options = new DbContextOptionsBuilder<ContentDbContext>();
            _sourceRegistry.ConfigureDbContext(options, source.Key);
            await using var dbContext = new ContentDbContext(options.Options);
            var sourceRepository = new DatabaseContentRepository(dbContext);
            var sourceItems = await sourceRepository.GetAllAsync(cancellationToken);

            foreach (var item in sourceItems)
            {
                item.SourceKey = source.Key;

                // Sources are ordered from base to override. Later sources replace matching stable IDs.
                if (itemsById.TryGetValue(item.Id, out var replacedItem))
                {
                    slugOwners.Remove(replacedItem.Slug);
                }

                // A later source may also intentionally move a route to a stable ID that previously belonged
                // to another page. The later source owns that slug in the composed catalog.
                if (slugOwners.TryGetValue(item.Slug, out var replacedId)
                    && !string.Equals(replacedId, item.Id, StringComparison.OrdinalIgnoreCase))
                {
                    itemsById.Remove(replacedId);
                }

                itemsById[item.Id] = item;
                slugOwners[item.Slug] = item.Id;
            }
        }

        return itemsById.Values.ToList();
    }

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var items = await GetAllAsync(cancellationToken);
        return items.SingleOrDefault(item => string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    private SiteModeContext GetSiteModeContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items[SiteModeContext.HttpContextItemKey] is SiteModeContext siteModeContext)
        {
            return siteModeContext;
        }

        return new SiteModeContext
        {
            SiteMode = SiteMode.Development,
            IsDevelopmentPreview = true
        };
    }
}
