using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

public interface IContentCatalogService
{
    Task<IReadOnlyList<ContentItem>> GetByContextAsync(
        string contextTag,
        SiteModeContext modeContext,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default);

    Task<ContentItem?> GetForDetailAsync(
        string slug,
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default);

    Task<ContentItem?> GetForDetailByIdAsync(
        string contentKey,
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default);

    // Compatibility overloads for consumers that still cross the legacy SiteMode boundary.
    // New code should pass the request's SiteModeContext so stable registered ids remain the
    // source of truth. Remove these once the remaining media/test callers have migrated.
    Task<IReadOnlyList<ContentItem>> GetByContextAsync(
        string contextTag,
        SiteMode siteMode,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default);

    Task<ContentItem?> GetForDetailAsync(
        string slug,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default);

    Task<ContentItem?> GetForDetailByIdAsync(
        string contentKey,
        SiteMode siteMode,
        bool isDevelopmentPreview,
        CancellationToken cancellationToken = default);
}
