using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentStorageSchemaTests
{
    [Fact]
    public async Task InitializerAddsMediaWikiStyleTablesToAnExistingContentDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"content-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var databasePath = Path.Combine(directory, "legacy.db");
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE content_page (page_id INTEGER NOT NULL PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            await CreateInitializer(directory).InitializeAsync();

            await using var verifyConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await verifyConnection.OpenAsync();
            await using var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('content_asset', 'content_page_asset', 'content_revision_asset', 'content_page_asset_dependency')
                """;
            Assert.Equal(4L, (long)(await verifyCommand.ExecuteScalarAsync())!);

            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'index'
                  AND name IN (
                      'IX_content_asset_asset_key',
                      'IX_content_asset_asset_sha256',
                      'IX_content_page_asset_asset_id',
                      'IX_content_revision_asset_asset_key',
                      'IX_content_page_asset_dependency_asset_key')
                """;
            Assert.Equal(5L, (long)(await verifyCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ContentStorageInitializer CreateInitializer(string directory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LegacyDb"] = "Data Source=legacy.db;Pooling=False",
                ["ContentStorage:AuthoringSource"] = "Legacy",
                ["ContentStorage:Sources:Legacy:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Legacy:ConnectionString"] = "LegacyDb"
            })
            .Build();
        return new ContentStorageInitializer(new ContentSourceRegistry(configuration, directory));
    }
}
