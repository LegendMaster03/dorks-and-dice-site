using dorks_and_dice_site.Models.Content;
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
}
