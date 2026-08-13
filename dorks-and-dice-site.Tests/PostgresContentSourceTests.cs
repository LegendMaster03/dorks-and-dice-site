using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class PostgresContentSourceTests
{
    [Fact]
    public void RegistryConfiguresNpgsqlProvider()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgresDb"] = "Host=localhost;Port=5432;Database=content;Username=content;Password=test",
            ["ContentStorage:AuthoringSource"] = "External",
            ["ContentStorage:Sources:External:Provider"] = "PostgreSQL",
            ["ContentStorage:Sources:External:ConnectionString"] = "PostgresDb"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());
        var options = new DbContextOptionsBuilder<ContentDbContext>();

        registry.ConfigureDbContext(options, "External");
        using var context = new ContentDbContext(options.Options);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
