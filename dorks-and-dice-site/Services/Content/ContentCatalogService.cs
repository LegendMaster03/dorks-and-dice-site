using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentCatalogService : IContentCatalogService
{
    private readonly IContentRepository _repository;

    public ContentCatalogService(IContentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ContentItem>> GetByContextAsync(
        string contextTag,
        SiteMode siteMode,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items
            .Where(item => item.HasTag(contextTag))
            .Where(item => includeUnlisted || item.IsListed)
            .Where(item => item.IsVisibleInMode(siteMode))
            .ToList();
    }

    public async Task<ContentItem?> GetForDetailAsync(
        string slug,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return isDevelopmentPreview || item.IsVisibleInMode(siteMode)
            ? item
            : null;
    }

    public async Task<ContentItem?> GetForDetailByIdAsync(
        string contentKey,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        var item = items.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, contentKey, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        return isDevelopmentPreview || item.IsVisibleInMode(siteMode)
            ? item
            : null;
    }
}
