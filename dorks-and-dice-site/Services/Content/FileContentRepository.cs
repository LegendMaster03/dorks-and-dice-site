using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public sealed class FileContentRepository : IContentRepository
{
    private readonly Lazy<IReadOnlyList<ContentItem>> _items;

    public FileContentRepository(IWebHostEnvironment environment)
    {
        _items = new Lazy<IReadOnlyList<ContentItem>>(
            () => ContentFileLoader.LoadAll(environment.ContentRootPath),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items.Value);
    }

    public Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _items.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }
}
