using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Services.Operator;

public static class OperatorServiceCollectionExtensions
{
    public static IServiceCollection AddOperatorInterface(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, OperatorAuthenticationHandler>(
                OperatorAuthenticationDefaults.Scheme,
                _ => { });
        services.AddScoped<IOperatorCredentialService, OperatorCredentialService>();
        services.AddScoped<IOperatorBrowserBootstrapService, OperatorBrowserBootstrapService>();
        services.AddScoped<IOperatorContentAccessService, OperatorContentAccessService>();
        services.AddScoped<OperatorAuditFilter>();
        services.AddSingleton<IOperatorCapabilityRegistry, OperatorCapabilityRegistry>();
        services.AddHostedService<OperatorProvisioningHostedService>();
        services.AddScoped<SignInManager<ApplicationUser>, ApplicationSignInManager>();
        return services;
    }
}
