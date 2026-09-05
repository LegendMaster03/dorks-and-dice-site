using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Site;

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
        SiteModeContext modeContext,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items
            .Where(item => item.HasTag(contextTag))
            .Where(item => includeUnlisted || item.IsListed)
            .Where(item => IsEligibleForListing(item, modeContext))
            .ToList();
    }

    public async Task<ContentItem?> GetForDetailAsync(
        string slug,
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return CanInspectDetail(item, modeContext) ? item : null;
    }

    public async Task<ContentItem?> GetForDetailByIdAsync(
        string contentKey,
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        var item = items.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, contentKey, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        return CanInspectDetail(item, modeContext) ? item : null;
    }

    private static bool IsEligibleForListing(ContentItem item, SiteModeContext modeContext)
    {
        if (modeContext.ActiveModeId is not null)
        {
            return item.IsVisibleInMode(modeContext.ActiveModeId);
        }

        // Trusted Preview with no selected site preserves the former Development-mode
        // inspection behavior. Framework fallback on an unmapped public host has no content
        // identity and therefore lists nothing.
        return modeContext.IsTrustedPreview;
    }

    private static bool CanInspectDetail(ContentItem item, SiteModeContext modeContext)
    {
        if (modeContext.IsDevelopmentPreview)
        {
            return true;
        }

        return IsEligibleForListing(item, modeContext);
    }
}
