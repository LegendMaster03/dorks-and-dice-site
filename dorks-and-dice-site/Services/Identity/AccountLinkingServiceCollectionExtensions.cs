using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Services.Identity;

public static class AccountLinkingServiceCollectionExtensions
{
    public static IServiceCollection AddAccountLinking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAccountLinkProviderCatalog, AccountLinkProviderCatalog>();
        return services;
    }

    public static IServiceCollection AddOpenIddictAccountLinking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenIddict()
            .AddClient(options =>
            {
                // Account linking stores no provider access/refresh tokens. State tokens are
                // self-contained Data Protection payloads; one-time application nonces stored
                // in ASP.NET Identity preserve replay protection at the linking boundary.
                options.DisableTokenStorage();
                options.UseDataProtection();

                options.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough();

                options.UseSystemNetHttp();
                options.UseWebProviders();
            });

        return services;
    }

    public static IServiceCollection AddOAuthAccountLinkProvider(
        this IServiceCollection services,
        AccountLinkProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);

        services.AddSingleton<IAccountLinkProvider>(
            new OAuthAccountLinkProvider(descriptor));
        return services;
    }

    public static IServiceCollection AddOpenIddictAccountLinkProvider(
        this IServiceCollection services,
        AccountLinkProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);

        services.AddSingleton<IAccountLinkProvider>(
            new OpenIddictAccountLinkProvider(descriptor));
        return services;
    }
}
