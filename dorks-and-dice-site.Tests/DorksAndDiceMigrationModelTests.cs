using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceMigrationModelTests
{
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("PostgreSQL")]
    public void MigrationSnapshotMatchesCurrentProviderIndependentModel(string provider)
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
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var snapshot = Assert.IsType<dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations.DorksAndDiceDbContextModelSnapshot>(
            migrationsAssembly.ModelSnapshot);
        var currentModel = db.GetService<IDesignTimeModel>().Model;

        Assert.False(DorksAndDiceMigrationModelGuard.HasDifferences(snapshot.Model, currentModel));
    }
}
