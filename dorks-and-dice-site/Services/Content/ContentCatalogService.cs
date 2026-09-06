using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
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

    public Task<IReadOnlyList<ContentItem>> GetByContextAsync(
        string contextTag,
        SiteMode siteMode,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default) =>
        GetByContextAsync(
            contextTag,
            FromLegacyMode(siteMode, isDevelopmentPreview: siteMode == SiteMode.Development),
            includeUnlisted,
            cancellationToken);

    public Task<ContentItem?> GetForDetailAsync(
        string slug,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default) =>
        GetForDetailAsync(
            slug,
            FromLegacyMode(siteMode, isDevelopmentPreview),
            cancellationToken);

    public Task<ContentItem?> GetForDetailByIdAsync(
        string contentKey,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default) =>
        GetForDetailByIdAsync(
            contentKey,
            FromLegacyMode(siteMode, isDevelopmentPreview),
            cancellationToken);

    private static bool IsEligibleForListing(ContentItem item, SiteModeContext modeContext)
    {
        // Synthetic Development spans all normal mode assignments. Database/source composition
        // has already happened below this service, so this does not bypass source restrictions.
        if (modeContext.SyntheticMode is not null)
        {
            return true;
        }

        if (modeContext.ActiveModeId is { Length: > 0 } activeModeId)
        {
            return item.IsVisibleInMode(activeModeId);
        }

        // Framework fallback has no content identity and therefore lists nothing.
        return false;
    }

    private static bool CanInspectDetail(ContentItem item, SiteModeContext modeContext) =>
        IsEligibleForListing(item, modeContext);

    private static SiteModeContext FromLegacyMode(SiteMode siteMode, bool isDevelopmentPreview)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var mode))
        {
            return new SiteModeContext
            {
                ActiveMode = mode,
                IsDevelopmentPreview = isDevelopmentPreview
            };
        }

        if (siteMode == SiteMode.Development)
        {
            return new SiteModeContext
            {
                FrameworkState = SyntheticSiteModes.Development,
                HasTrustedAccess = true,
                IsDevelopmentPreview = isDevelopmentPreview
            };
        }

        return new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.Fallback,
            IsDevelopmentPreview = isDevelopmentPreview
        };
    }
}
