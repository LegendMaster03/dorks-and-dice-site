using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public static class ContentStorageServiceCollectionExtensions
{
    public static IServiceCollection AddContentStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var profileName = configuration["ContentStorage:Profile"] ?? "External";
        var profile = configuration.GetSection($"ContentStorage:Profiles:{profileName}");
        var provider = profile["Provider"]
            ?? throw new InvalidOperationException($"Content storage profile '{profileName}' does not define a provider.");
        var connectionStringName = profile["ConnectionString"]
            ?? throw new InvalidOperationException($"Content storage profile '{profileName}' does not define a connection string name.");
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found for content storage profile '{profileName}'.");

        services.AddDbContext<ContentDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Content database provider '{provider}' is not supported by this build.");
            }
        });

        services.AddScoped<IContentRepository, DatabaseContentRepository>();
        services.AddScoped<IContentCatalogService, ContentCatalogService>();
        services.AddSingleton<IContentDirectiveRenderer, SiteModeArchitectureDirectiveRenderer>();
        services.AddSingleton<IContentBodyRenderer, ContentBodyRenderer>();

        return services;
    }
}
