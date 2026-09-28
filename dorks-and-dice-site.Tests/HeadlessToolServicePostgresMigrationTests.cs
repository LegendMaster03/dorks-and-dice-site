using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HeadlessToolServicePostgresMigrationTests
{
    [Fact]
    public async Task LegacyPostgresRegistrationMigratesStableKeyWithoutChangingPublicBehavior()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        var schema = $"headless_tool_{Guid.NewGuid():N}";
        var schemaConnectionString = WithSchema(baseConnectionString, schema);

        await using (var admin = new NpgsqlConnection(baseConnectionString))
        {
            await admin.OpenAsync();
            await using var createSchema = admin.CreateCommand();
            createSchema.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            await using (var connection = new NpgsqlConnection(schemaConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    -- A legacy content database already has the core page/revision tables. The
                    -- schema initializer adds auxiliary media/dependency tables around them before
                    -- migrating Tool registration state.
                    CREATE TABLE content_page (
                        page_id bigint NOT NULL PRIMARY KEY);
                    CREATE TABLE content_revision (
                        revision_id bigint NOT NULL PRIMARY KEY);

                    CREATE TABLE tool_registration (
                        tool_id uuid NOT NULL PRIMARY KEY,
                        tool_slug text NOT NULL,
                        tool_display_name text NOT NULL,
                        tool_description text NULL,
                        tool_integration_type smallint NOT NULL DEFAULT 0,
                        tool_integration_contract_version integer NULL,
                        tool_upstream_base_url text NULL,
                        tool_frontend_entry_point text NULL,
                        tool_health_path text NULL,
                        tool_modes text[] NOT NULL DEFAULT ARRAY[]::text[],
                        tool_delegation_targets text[] NOT NULL DEFAULT ARRAY[]::text[],
                        tool_allow_anonymous boolean NOT NULL DEFAULT true,
                        tool_enabled boolean NOT NULL DEFAULT false,
                        tool_created_at timestamp with time zone NOT NULL,
                        tool_updated_at timestamp with time zone NOT NULL,
                        CONSTRAINT ck_tool_registration_integration_type
                            CHECK (tool_integration_type IN (0, 1)),
                        CONSTRAINT ck_tool_registration_slug_not_empty
                            CHECK (length(btrim(tool_slug)) > 0));
                    CREATE UNIQUE INDEX ux_tool_registration_slug_ci
                        ON tool_registration(lower(tool_slug));
                    INSERT INTO tool_registration (
                        tool_id, tool_slug, tool_display_name, tool_description,
                        tool_integration_type, tool_integration_contract_version,
                        tool_upstream_base_url, tool_frontend_entry_point, tool_health_path,
                        tool_modes, tool_delegation_targets, tool_allow_anonymous, tool_enabled,
                        tool_created_at, tool_updated_at)
                    VALUES (
                        '11111111-1111-1111-1111-111111111111',
                        'rules-core',
                        'Rules Core',
                        'Legacy production-shaped registration',
                        0,
                        2,
                        'http://rules-core:8080',
                        '/app.js',
                        '/ready',
                        ARRAY['dorks-and-dice']::text[],
                        ARRAY['character-sheet']::text[],
                        false,
                        true,
                        '2026-09-01T00:00:00Z',
                        '2026-09-02T00:00:00Z');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var sourceRegistry = CreateSourceRegistry(schemaConnectionString);
            await new ContentStorageInitializer(sourceRegistry).InitializeAsync();
            var registry = new DatabaseToolRegistry(sourceRegistry);

            var byKey = Assert.IsType<ToolRegistration>(await registry.GetByKeyAsync("RULES-CORE"));
            var bySlug = Assert.IsType<ToolRegistration>(await registry.GetBySlugAsync("RULES-CORE"));

            Assert.Equal(byKey.Id, bySlug.Id);
            Assert.Equal("rules-core", byKey.Key);
            Assert.Equal("rules-core", byKey.Slug);
            Assert.Equal(ToolKind.Application, byKey.Kind);
            Assert.Equal(ToolIntegrationType.EmbeddedModule, byKey.IntegrationType);
            Assert.Equal(ToolIntegrationContractVersions.EmbeddedModuleCurrent, byKey.IntegrationContractVersion);
            Assert.Equal("http://rules-core:8080", byKey.UpstreamBaseUrl);
            Assert.Equal("/app.js", byKey.FrontendEntryPoint);
            Assert.Equal("/ready", byKey.HealthPath);
            Assert.Equal(new[] { "dorks-and-dice" }, byKey.Modes);
            Assert.Equal(new[] { "character-sheet" }, byKey.DelegationTargets);
            Assert.False(byKey.AllowAnonymous);
            Assert.True(byKey.Enabled);
        }
        finally
        {
            await using var admin = new NpgsqlConnection(baseConnectionString);
            await admin.OpenAsync();
            await using var dropSchema = admin.CreateCommand();
            dropSchema.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }

    private static string WithSchema(string connectionString, string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schema
        };
        return builder.ConnectionString;
    }

    private static ContentSourceRegistry CreateSourceRegistry(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HeadlessPostgresMigration"] = connectionString,
                ["ContentStorage:AuthoringSource"] = "HeadlessPostgresMigration",
                ["ContentStorage:Sources:HeadlessPostgresMigration:DisplayName"] = "Headless PostgreSQL migration",
                ["ContentStorage:Sources:HeadlessPostgresMigration:Provider"] = "PostgreSQL",
                ["ContentStorage:Sources:HeadlessPostgresMigration:ConnectionString"] = "HeadlessPostgresMigration"
            })
            .Build();
        return new ContentSourceRegistry(configuration, AppContext.BaseDirectory);
    }
}
