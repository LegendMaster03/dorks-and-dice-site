using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public static class ContentStorageServiceCollectionExtensions
{
    public static IServiceCollection AddContentStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var sourceRegistry = new ContentSourceRegistry(configuration, contentRootPath);

        services.AddSingleton<IContentSourceRegistry>(sourceRegistry);
        services.AddHttpContextAccessor();
        services.AddDbContext<ContentDbContext>(options =>
            sourceRegistry.ConfigureDbContext(options, sourceRegistry.AuthoringSourceKey));

        services.AddScoped<IContentRepository, CompositeContentRepository>();
        services.AddScoped<IContentCatalogService, ContentCatalogService>();
        services.AddScoped<IContentAuthoringService, ContentAuthoringService>();
        services.AddScoped<IContentSourceTransferService, ContentSourceTransferService>();
        services.AddSingleton<IContentDirectiveRenderer, SiteModeArchitectureDirectiveRenderer>();
        services.AddSingleton<IContentBodyRenderer, ContentBodyRenderer>();

        return services;
    }
}
