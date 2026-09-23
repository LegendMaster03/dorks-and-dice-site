using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Services.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client.WebIntegration;

namespace dorks_and_dice_site.Plugins.AccountLinks.Discord;

public sealed class DiscordAccountLinkPlugin : ISitePlugin
{
    public const string ProviderId = "discord";
    public const string ProviderDisplayName = "Discord";
    public const string AuthenticationScheme =
        OpenIddictClientWebIntegrationConstants.Providers.Discord;

    private readonly IConfiguration _configuration;

    public DiscordAccountLinkPlugin(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SitePluginManifest Manifest { get; } = new(
        Id: "discord-account-link",
        DisplayName: "Discord Account Link",
        Version: "0.1.0");

    public void RegisterServices(IServiceCollection services)
    {
        var section = _configuration.GetSection("AccountLinks:Discord");
        if (!section.GetValue<bool>("Enabled"))
        {
            return;
        }

        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "AccountLinks:Discord requires ClientId and ClientSecret when enabled.");
        }

        services.AddOpenIddict()
            .AddClient(options =>
            {
                options.UseWebProviders()
                    .AddDiscord(discord =>
                    {
                        discord.SetClientId(clientId)
                            .SetClientSecret(clientSecret)
                            .SetRedirectUri("account/links/callback/discord");
                    });
            });

        services.AddOpenIddictAccountLinkProvider(
            new AccountLinkProviderDescriptor(
                ProviderId,
                ProviderDisplayName,
                AuthenticationScheme,
                AccountLinkProtocol.OpenIddict));
    }
}
