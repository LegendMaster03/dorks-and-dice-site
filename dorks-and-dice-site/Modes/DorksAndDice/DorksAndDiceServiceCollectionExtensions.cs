using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Modes.DorksAndDice;

public static class DorksAndDiceServiceCollectionExtensions
{
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
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var provider = configuration[$"{DorksAndDiceStorageOptions.SectionName}:Provider"] ?? "Sqlite";
            var configuredConnectionString =
                configuration.GetConnectionString("DorksAndDice")
                ?? configuration[$"{DorksAndDiceStorageOptions.SectionName}:ConnectionString"];

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
        });

        services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<ICampaignParticipantService, CampaignParticipantService>();
        services.AddScoped<ICampaignInvitationService, CampaignInvitationService>();
        services.AddScoped<ICampaignContextService, CampaignContextService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddHostedService<DorksAndDiceStorageInitializer>();
        return services;
    }
}
