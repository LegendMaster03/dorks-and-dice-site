using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Modes.DorksAndDice;

public static class DorksAndDiceServiceCollectionExtensions
{
    private const string SharedMigrationsHistoryTable = "__DorksAndDiceMigrationsHistory";

    /// <summary>
    /// Registers services owned by the Dorks & Dice normal mode. Campaign, character,
    /// and other Dorks-specific domain services are composed here rather than in generic
    /// framework startup code.
    /// </summary>
    public static IServiceCollection AddDorksAndDiceMode(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISiteModePresentationModule, DorksAndDicePresentationModule>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<DorksAndDiceDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var configuredProvider = configuration[$"{DorksAndDiceStorageOptions.SectionName}:Provider"];
            var configuredConnectionString =
                configuration.GetConnectionString("DorksAndDice")
                ?? configuration[$"{DorksAndDiceStorageOptions.SectionName}:ConnectionString"];

            // An explicitly configured Dorks & Dice store always wins. This keeps the mode
            // independently deployable even when the default deployment shares the durable
            // External content database at the physical database level.
            if (!string.IsNullOrWhiteSpace(configuredProvider)
                || !string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                ConfigureExplicitStorage(
                    options,
                    configuredProvider ?? "Sqlite",
                    configuredConnectionString,
                    serviceProvider.GetRequiredService<IHostEnvironment>());
                return;
            }

            var contentSourceKey = configuration[$"{DorksAndDiceStorageOptions.SectionName}:ContentSource"];
            if (!string.IsNullOrWhiteSpace(contentSourceKey))
            {
                var contentSourceRegistry = serviceProvider.GetRequiredService<IContentSourceRegistry>();
                ConfigureSharedContentSource(options, contentSourceRegistry.GetSource(contentSourceKey));
                return;
            }

            ConfigureExplicitStorage(
                options,
                "Sqlite",
                configuredConnectionString: null,
                serviceProvider.GetRequiredService<IHostEnvironment>());
        });

        services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<ICampaignParticipantService, CampaignParticipantService>();
        services.AddScoped<ICampaignInvitationService, CampaignInvitationService>();
        services.AddScoped<ICampaignContextService, CampaignContextService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IDorksAndDiceDiscordServerService, DorksAndDiceDiscordServerService>();
        services.AddScoped<dorks_and_dice_site.Plugins.DiscordBot.IDiscordWorkspaceProjectionSource, DorksAndDiceDiscordCampaignProjectionSource>();
        services.AddScoped<dorks_and_dice_site.Plugins.DiscordBot.IDiscordWorkspaceProjectionSource, DorksAndDiceDiscordLinkedAccountProjectionSource>();
        services.AddScoped<IDorksAndDiceDeletionService, DorksAndDiceDeletionService>();
        services.AddScoped<IToolLifecycleOutboxDispatcher, ToolLifecycleOutboxDispatcher>();
        services.AddHostedService<DorksAndDiceStorageInitializer>();
        services.AddHostedService<ToolLifecycleDeliveryWorker>();
        return services;
    }

    private static void ConfigureExplicitStorage(
        DbContextOptionsBuilder options,
        string provider,
        string? configuredConnectionString,
        IHostEnvironment environment)
    {
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuredConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var contentDirectory = Path.Combine(environment.ContentRootPath, "Content");
                Directory.CreateDirectory(contentDirectory);
                connectionString = $"Data Source={Path.Combine(contentDirectory, "dorks-and-dice.db")}";
            }

            options.UseSqlite(connectionString);
            return;
        }

        if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                throw new InvalidOperationException(
                    "DorksAndDiceStorage requires a connection string when PostgreSQL is selected.");
            }

            options.UseNpgsql(configuredConnectionString);
            return;
        }

        throw new NotSupportedException($"Dorks & Dice storage provider '{provider}' is not supported.");
    }

    private static void ConfigureSharedContentSource(
        DbContextOptionsBuilder options,
        ContentSourceDefinition contentSource)
    {
        switch (contentSource.Provider.ToLowerInvariant())
        {
            case "sqlite":
                options.UseSqlite(
                    contentSource.ConnectionString,
                    sqlite => sqlite.MigrationsHistoryTable(SharedMigrationsHistoryTable));
                return;
            case "postgres":
            case "postgresql":
                options.UseNpgsql(
                    contentSource.ConnectionString,
                    postgres => postgres.MigrationsHistoryTable(SharedMigrationsHistoryTable));
                return;
            default:
                throw new NotSupportedException(
                    $"Content source '{contentSource.Key}' uses provider '{contentSource.Provider}', which Dorks & Dice storage does not support.");
        }
    }
}
