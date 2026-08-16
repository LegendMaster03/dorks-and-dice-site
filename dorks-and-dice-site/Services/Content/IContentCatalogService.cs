using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Content;

public interface IContentCatalogService
{
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
