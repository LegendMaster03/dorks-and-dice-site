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

        if (services.Any(static descriptor =>
            descriptor.ServiceType == typeof(OpenIddictAccountLinkingRegistrationMarker)))
        {
            return services;
        }

        services.AddSingleton(new OpenIddictAccountLinkingRegistrationMarker());

        services.AddOpenIddict()
            .AddClient(options =>
            {
                // Account linking stores no provider access/refresh tokens. State tokens are
                // self-contained Data Protection payloads; one-time application nonces stored
                // in ASP.NET Identity preserve replay protection at the linking boundary.
                options.DisableTokenStorage();
                options.UseDataProtection();

                // OpenIddict requires an encryption credential for interactive client
                // operations. Account links are transient and provider access/refresh
                // tokens are never persisted, so a process-local credential is sufficient:
                // a restart may invalidate an in-flight link attempt, but never an
                // established account link.
                options.AddEphemeralEncryptionKey();
                options.AddEphemeralSigningKey();

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

        services.AddOpenIddictAccountLinking();
        services.AddSingleton<IAccountLinkProvider>(
            new OpenIddictAccountLinkProvider(descriptor));
        return services;
    }
}


internal sealed class OpenIddictAccountLinkingRegistrationMarker
{
}
