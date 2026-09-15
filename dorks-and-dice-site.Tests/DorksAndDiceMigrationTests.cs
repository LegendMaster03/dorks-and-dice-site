using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DorksAndDiceMigrationTests
{
    [Fact]
    public async Task InitialMigrationCreatesUsableCampaignSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>().UseSqlite(connection).Options;
        await using var db = new DorksAndDiceDbContext(options);

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260914223500_InitialDorksAndDice", applied);
        Assert.False(await db.Campaigns.AnyAsync());
        Assert.False(await db.Characters.AnyAsync());
    }

    [Fact]
    public async Task InitialMigrationCreatesUsableCampaignSchemaOnPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"dd_campaign_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync();

        await using (var createSchema = adminConnection.CreateCommand())
        {
            createSchema.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            var testConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            };
            var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseNpgsql(testConnection.ConnectionString)
                .Options;
            await using var db = new DorksAndDiceDbContext(options);

            await db.Database.MigrateAsync();

            var applied = await db.Database.GetAppliedMigrationsAsync();
            Assert.Contains("20260914223500_InitialDorksAndDice", applied);
            Assert.False(await db.Campaigns.AnyAsync());
            Assert.False(await db.CampaignInvitations.AnyAsync());
            Assert.False(await db.Characters.AnyAsync());
        }
        finally
        {
            await using var dropSchema = adminConnection.CreateCommand();
            dropSchema.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
