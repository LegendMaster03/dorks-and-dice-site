using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentStorageSchemaTests
{
    [Fact]
    public async Task InitializerAddsMediaAndRedirectTablesToAnExistingContentDatabase()
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
            await CreateInitializer(directory).InitializeAsync();

            await using var verifyConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await verifyConnection.OpenAsync();
            await using var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN (
                      'content_asset',
                      'content_page_asset',
                      'content_revision_asset',
                      'content_page_asset_dependency',
                      'content_redirect')
                """;
            Assert.Equal(5L, (long)(await verifyCommand.ExecuteScalarAsync())!);

            verifyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'index'
                  AND name IN (
                      'IX_content_asset_asset_key',
                      'IX_content_asset_asset_sha256',
                      'IX_content_page_asset_asset_id',
                      'IX_content_revision_asset_asset_key',
                      'IX_content_page_asset_dependency_asset_key',
                      'IX_content_redirect_redirect_namespace_redirect_slug',
                      'IX_content_redirect_redirect_page_id')
                """;
            Assert.Equal(7L, (long)(await verifyCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InitializerAddsDelegationTargetsAndSeedsCharacterSheetRelationship()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"content-schema-delegation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var databasePath = Path.Combine(directory, "legacy.db");
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE content_page (page_id INTEGER NOT NULL PRIMARY KEY);
                    CREATE TABLE tool_registration (
                        tool_id TEXT NOT NULL PRIMARY KEY,
                        tool_slug TEXT NOT NULL,
                        tool_display_name TEXT NOT NULL,
                        tool_description TEXT NULL,
                        tool_integration_type INTEGER NOT NULL DEFAULT 0,
                        tool_integration_contract_version INTEGER NULL,
                        tool_upstream_base_url TEXT NULL,
                        tool_frontend_entry_point TEXT NULL,
                        tool_health_path TEXT NULL,
                        tool_modes TEXT NOT NULL DEFAULT '[]',
                        tool_allow_anonymous INTEGER NOT NULL DEFAULT 1,
                        tool_enabled INTEGER NOT NULL DEFAULT 0,
                        tool_created_at TEXT NOT NULL,
                        tool_updated_at TEXT NOT NULL);
                    INSERT INTO tool_registration
                        (tool_id, tool_slug, tool_display_name, tool_integration_type,
                         tool_integration_contract_version, tool_modes, tool_allow_anonymous,
                         tool_enabled, tool_created_at, tool_updated_at)
                    VALUES
                        ('11111111-1111-1111-1111-111111111111',
                         'character-sheet',
                         'Character Sheet',
                         0,
                         2,
                         '["dorks-and-dice"]',
                         0,
                         1,
                         '2026-09-18T00:00:00.0000000+00:00',
                         '2026-09-18T00:00:00.0000000+00:00');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await CreateInitializer(directory).InitializeAsync();
            await CreateInitializer(directory).InitializeAsync();

            await using var verifyConnection =
                new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await verifyConnection.OpenAsync();
            await using var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText = """
                SELECT tool_delegation_targets
                FROM tool_registration
                WHERE tool_slug = 'character-sheet'
                """;

            Assert.Equal(
                "[\"rules-core\"]",
                (string)(await verifyCommand.ExecuteScalarAsync())!);
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
