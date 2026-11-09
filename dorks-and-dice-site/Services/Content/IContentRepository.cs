using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Reads current-revision pages from the composed content source collection;
/// the catalog service subsequently enforces request visibility.
/// </summary>
public interface IContentRepository
{
    Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
