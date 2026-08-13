using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public interface IContentRepository
{
    Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
