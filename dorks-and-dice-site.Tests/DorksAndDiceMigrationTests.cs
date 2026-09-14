using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

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
}
