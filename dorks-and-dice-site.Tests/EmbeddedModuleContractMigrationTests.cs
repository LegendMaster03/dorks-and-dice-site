using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class EmbeddedModuleContractMigrationTests
{
    [Fact]
    public async Task LegacyRegistryAddsNullableContractVersionAndMigratesOnlyKnownEmbeddedModules()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tool-contract-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "legacy.db");

        try
        {
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
                        tool_upstream_base_url TEXT NULL,
                        tool_frontend_entry_point TEXT NULL,
                        tool_health_path TEXT NULL,
                        tool_modes TEXT NOT NULL DEFAULT '[]',
                        tool_allow_anonymous INTEGER NOT NULL DEFAULT 1,
                        tool_enabled INTEGER NOT NULL DEFAULT 0,
                        tool_created_at TEXT NOT NULL,
                        tool_updated_at TEXT NOT NULL);
                    INSERT INTO tool_registration
                        (tool_id, tool_slug, tool_display_name, tool_integration_type, tool_modes,
                         tool_allow_anonymous, tool_enabled, tool_created_at, tool_updated_at)
                    VALUES
                        ('11111111-1111-1111-1111-111111111111', 'block-initiative', 'Block Initiative', 0, '[]', 1, 1, '2026-09-13T00:00:00Z', '2026-09-13T00:00:00Z'),
                        ('22222222-2222-2222-2222-222222222222', 'rules-core', 'Rules Core', 0, '[]', 1, 1, '2026-09-13T00:00:00Z', '2026-09-13T00:00:00Z'),
                        ('33333333-3333-3333-3333-333333333333', 'legacy-third-tool', 'Legacy Third Tool', 0, '[]', 1, 1, '2026-09-13T00:00:00Z', '2026-09-13T00:00:00Z'),
                        ('44444444-4444-4444-4444-444444444444', 'proxied-tool', 'Proxied Tool', 1, '[]', 1, 1, '2026-09-13T00:00:00Z', '2026-09-13T00:00:00Z');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var initializer = CreateInitializer(directory);
            await initializer.InitializeAsync();
            await initializer.InitializeAsync();

            await using var verifyConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await verifyConnection.OpenAsync();

            await using (var columnCommand = verifyConnection.CreateCommand())
            {
                columnCommand.CommandText = """
                    SELECT COUNT(*)
                    FROM pragma_table_info('tool_registration')
                    WHERE name = 'tool_integration_contract_version'
                    """;
                Assert.Equal(1L, (long)(await columnCommand.ExecuteScalarAsync())!);
            }

            var versions = new Dictionary<string, int?>();
            await using (var versionCommand = verifyConnection.CreateCommand())
            {
                versionCommand.CommandText = """
                    SELECT tool_slug, tool_integration_contract_version
                    FROM tool_registration
                    ORDER BY tool_slug
                    """;
                await using var reader = await versionCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    versions[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                }
            }

            Assert.Equal(2, versions["block-initiative"]);
            Assert.Equal(2, versions["rules-core"]);
            Assert.Null(versions["legacy-third-tool"]);
            Assert.Null(versions["proxied-tool"]);
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
