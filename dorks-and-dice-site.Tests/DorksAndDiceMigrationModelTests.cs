using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceMigrationModelTests
{
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("PostgreSQL")]
    public void MigrationSnapshotMatchesCurrentModel(string provider)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DorksAndDiceDbContext>();
        if (provider == "Sqlite")
        {
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }
        else
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused");
        }

        using var db = new DorksAndDiceDbContext(optionsBuilder.Options);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
