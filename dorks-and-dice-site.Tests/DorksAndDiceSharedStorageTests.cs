using dorks_and_dice_site.Modes.DorksAndDice;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceSharedStorageTests
{
    [Fact]
    public async Task ExternalContentSourcePersistsCampaignsAndCharactersAcrossProviderRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dorks-shared-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "external.db");
        var userId = Guid.NewGuid();
        Guid campaignId;
        Guid characterId;

        try
        {
            await using (var firstProvider = BuildProvider(databasePath))
            {
                using var scope = firstProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DorksAndDiceDbContext>();
                await db.Database.MigrateAsync();

                var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();
                var characterService = scope.ServiceProvider.GetRequiredService<ICharacterService>();
                campaignId = (await campaignService.CreateAsync(userId, "Persistent Campaign")).Id;
                characterId = (await characterService.CreateAsync(userId, "Persistent Character")).Id;
            }

            await using (var secondProvider = BuildProvider(databasePath))
            {
                using var scope = secondProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DorksAndDiceDbContext>();
                await db.Database.MigrateAsync();

                Assert.True(await db.Campaigns.AnyAsync(campaign => campaign.Id == campaignId));
                Assert.True(await db.Characters.AnyAsync(character => character.Id == characterId));
            }

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__DorksAndDiceMigrationsHistory';";
            var historyTableCount = Convert.ToInt64(await command.ExecuteScalarAsync());
            Assert.Equal(1, historyTableCount);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildProvider(string databasePath)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ExternalTest"] = $"Data Source={databasePath};Pooling=False",
            ["ContentStorage:AuthoringSource"] = "External",
            ["ContentStorage:Sources:External:DisplayName"] = "External content",
            ["ContentStorage:Sources:External:Provider"] = "Sqlite",
            ["ContentStorage:Sources:External:ConnectionString"] = "ExternalTest",
            ["ContentStorage:GlobalSources:0"] = "External",
            ["DorksAndDiceStorage:ContentSource"] = "External",
            ["DorksAndDiceStorage:MigrateOnStartup"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddContentStorage(configuration, Path.GetDirectoryName(databasePath)!);
        services.AddDorksAndDiceMode();
        return services.BuildServiceProvider();
    }
}
