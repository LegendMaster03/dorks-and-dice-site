using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentStorageSchemaTests
{
    [Fact]
    public async Task InitializerAddsManagedMediaTableToAnExistingContentDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"content-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var databasePath = Path.Combine(directory, "legacy.db");
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE content_page (page_id INTEGER NOT NULL PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LegacyDb"] = "Data Source=legacy.db",
                    ["ContentStorage:AuthoringSource"] = "Legacy",
                    ["ContentStorage:Sources:Legacy:Provider"] = "Sqlite",
                    ["ContentStorage:Sources:Legacy:ConnectionString"] = "LegacyDb"
                })
                .Build();
            var registry = new ContentSourceRegistry(configuration, directory);
            var initializer = new ContentStorageInitializer(registry);

            await initializer.InitializeAsync();

            await using var verifyConnection = new SqliteConnection($"Data Source={databasePath}");
            await verifyConnection.OpenAsync();
            await using var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'content_asset'
                """;
            Assert.Equal(1L, (long)(await verifyCommand.ExecuteScalarAsync())!);

            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'index'
                  AND name IN (
                      'IX_content_asset_asset_key',
                      'IX_content_asset_asset_page_id_asset_sha256')
                """;
            Assert.Equal(2L, (long)(await verifyCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
