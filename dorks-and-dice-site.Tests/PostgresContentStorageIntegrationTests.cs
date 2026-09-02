using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PostgresContentStorageIntegrationTests
{
    [Fact]
    public async Task InitializerCreatesCurrentSchemaAndIsIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgresTestDb"] = connectionString,
                ["ContentStorage:AuthoringSource"] = "PostgresTest",
                ["ContentStorage:Sources:PostgresTest:Provider"] = "PostgreSQL",
                ["ContentStorage:Sources:PostgresTest:ConnectionString"] = "PostgresTestDb"
            })
            .Build();
        var initializer = new ContentStorageInitializer(
            new ContentSourceRegistry(configuration, AppContext.BaseDirectory));

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN (
                  'content_page',
                  'content_revision',
                  'content_revision_tag',
                  'content_revision_mode',
                  'content_asset',
                  'content_page_asset',
                  'content_revision_asset',
                  'content_page_asset_dependency',
                  'content_redirect')
            """;

        Assert.Equal(9L, (long)(await command.ExecuteScalarAsync())!);
    }
}
