using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public interface IContentStorageInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class ContentStorageInitializer : IContentStorageInitializer
{
    private readonly IContentSourceRegistry _sourceRegistry;

    public ContentStorageInitializer(IContentSourceRegistry sourceRegistry)
    {
        _sourceRegistry = sourceRegistry;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var source in _sourceRegistry.GetAllSources())
        {
            var options = new DbContextOptionsBuilder<ContentDbContext>();
            _sourceRegistry.ConfigureDbContext(options, source.Key);
            await using var context = new ContentDbContext(options.Options);
            await ContentStorageSchema.EnsureCurrentAsync(context, cancellationToken);
        }
    }
}
