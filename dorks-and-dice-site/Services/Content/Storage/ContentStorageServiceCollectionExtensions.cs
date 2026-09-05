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
        services.AddSingleton<IContentStorageInitializer, ContentStorageInitializer>();
        services.AddHttpContextAccessor();
        services.AddDbContext<ContentDbContext>(options =>
            sourceRegistry.ConfigureDbContext(options, sourceRegistry.AuthoringSourceKey));

        services.AddScoped<IContentRepository, CompositeContentRepository>();
        services.AddScoped<IContentCatalogService, ContentCatalogService>();
        services.AddScoped<IHomepageContentService, HomepageContentService>();
        services.AddScoped<IContentRedirectService, ContentRedirectService>();
        services.AddScoped<IContentAuthoringService, ContentAuthoringService>();
        services.AddScoped<IContentSourceTransferService, ContentSourceTransferService>();
        services.AddScoped<IContentAssetService, ContentAssetService>();
        services.AddSingleton<IContentPageComponentDefinition, ContentCollectionPageComponentDefinition>();
        services.AddSingleton<IContentPageComposer, ContentPageComposer>();
        services.AddSingleton<IContentDirectiveRenderer, SiteModeArchitectureDirectiveRenderer>();
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "spoiler-warning-start",
            "<div class=\"alert alert-warning mt-4\" role=\"alert\">"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "content-note-start",
            "<aside class=\"content-note alert alert-secondary\" role=\"note\">"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "content-note-end",
            "</aside>"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "content-block-end",
            "</div>"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "spoiler-start",
            "<details class=\"content-spoiler border rounded p-3 mb-3\"><summary class=\"fw-semibold\">Reveal the ending</summary>"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "spoiler-end",
            "</details>"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "project-gallery-start",
            "<div class=\"project-gallery\">"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "project-gallery-end",
            "</div>"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "content-downloads-start",
            "<div class=\"content-downloads d-flex flex-column flex-md-row gap-2 mb-3\">"));
        services.AddSingleton<IContentDirectiveRenderer>(new StaticContentDirectiveRenderer(
            "content-downloads-end",
            "</div>"));
        services.AddSingleton<IContentBodyRenderer, ContentBodyRenderer>();

        return services;
    }
}
