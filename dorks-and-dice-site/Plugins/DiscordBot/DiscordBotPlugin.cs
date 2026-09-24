using System.Net.Http.Headers;
using dorks_and_dice_site.Framework.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public sealed class DiscordBotPlugin(IConfiguration configuration) : ISitePlugin
{
    public SitePluginManifest Manifest { get; } = new(
        Id: "discord-bot",
        DisplayName: "Discord Bot",
        Version: "0.1.0")
    {
        Dependencies = ["discord-account-link"]
    };

    public void RegisterServices(IServiceCollection services)
    {
        var section = configuration.GetSection(DiscordBotOptions.SectionName);
        var options = section.Get<DiscordBotOptions>() ?? new DiscordBotOptions();
        var clientId = options.ClientId
            ?? configuration["AccountLinks:Discord:ClientId"];

        services.AddSingleton(options);
        services.AddSingleton<IDiscordBotInstallLinkProvider>(
            new DiscordBotInstallLinkProvider(clientId, options.Enabled));

        if (!options.Enabled)
        {
            services.AddSingleton<IDiscordGuildOwnershipVerifier, DiscordGuildOwnershipVerifierUnavailable>();
            return;
        }

        var token = ResolveToken(options);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "DiscordBot requires Token or TokenFile when enabled.");
        }

        services.AddSingleton(new DiscordBotCredential(token, clientId));
        services.AddHttpClient<IDiscordBotClient, DiscordBotClient>((serviceProvider, client) =>
        {
            var credential = serviceProvider.GetRequiredService<DiscordBotCredential>();
            client.BaseAddress = new Uri("https://discord.com/api/v10/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bot", credential.Token);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DorksAndDiceSite/1.0 (+https://dorks-and-dice.com)");
        });
        services.AddScoped<IDiscordWorkspaceSyncService, DiscordWorkspaceSyncService>();
        services.AddScoped<IDiscordGuildOwnershipVerifier, DiscordGuildOwnershipVerifier>();
        services.AddHostedService<DiscordWorkspaceSyncWorker>();
    }

    private static string? ResolveToken(DiscordBotOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            return options.Token.Trim();
        }

        if (string.IsNullOrWhiteSpace(options.TokenFile))
        {
            return null;
        }

        if (!File.Exists(options.TokenFile))
        {
            throw new InvalidOperationException(
                $"Discord bot token file '{options.TokenFile}' does not exist.");
        }

        return File.ReadAllText(options.TokenFile).Trim();
    }
}
