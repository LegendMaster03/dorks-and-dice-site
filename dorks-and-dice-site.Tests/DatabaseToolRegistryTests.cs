using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class DatabaseToolRegistryTests
{
    [Fact]
    public async Task SqliteRegistrySupportsCrudCaseInsensitiveSlugAndModes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"tool-registry-{Guid.NewGuid():N}.db");
        try
        {
            var sourceRegistry = CreateSourceRegistry(
                "Sqlite",
                $"Data Source={databasePath};Pooling=False");
            await new ContentStorageInitializer(sourceRegistry).InitializeAsync();
            var registry = new DatabaseToolRegistry(sourceRegistry);

            var now = DateTimeOffset.UtcNow;
            var tool = new ToolRegistration
            {
                Id = Guid.NewGuid(),
                Slug = "rules-core-test",
                DisplayName = "Rules Core Test",
                Description = "Registry persistence test",
                IntegrationType = ToolIntegrationType.EmbeddedModule,
                UpstreamBaseUrl = "http://rules-core-test:8080",
                FrontendEntryPoint = "/app.js",
                HealthPath = "/ready",
                Modes = ["dorks-and-dice", "professional"],
                AllowAnonymous = false,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            await registry.SaveAsync(tool);

            var byId = await registry.GetByIdAsync(tool.Id);
            var bySlug = await registry.GetBySlugAsync("RULES-CORE-TEST");
            Assert.NotNull(byId);
            Assert.NotNull(bySlug);
            Assert.Equal(tool.Id, bySlug.Id);
            Assert.Equal(tool.Modes, bySlug.Modes);
            Assert.False(bySlug.AllowAnonymous);
            Assert.True(bySlug.Enabled);

            tool.DisplayName = "Updated Rules Core Test";
            tool.Modes = ["dorks-and-dice"];
            tool.Enabled = false;
            tool.UpdatedAt = now.AddMinutes(1);
            await registry.SaveAsync(tool);

            var updated = await registry.GetByIdAsync(tool.Id);
            Assert.NotNull(updated);
            Assert.Equal("Updated Rules Core Test", updated.DisplayName);
            Assert.Equal(new[] { "dorks-and-dice" }, updated.Modes);
            Assert.False(updated.Enabled);

            var duplicate = new ToolRegistration
            {
                Id = Guid.NewGuid(),
                Slug = "RULES-CORE-TEST",
                DisplayName = "Duplicate",
                CreatedAt = now,
                UpdatedAt = now
            };
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                registry.SaveAsync(duplicate));
            Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);

            var all = await registry.GetAllAsync();
            Assert.Single(all);
            Assert.Equal(tool.Id, all[0].Id);

            Assert.True(await registry.DeleteAsync(tool.Id));
            Assert.False(await registry.DeleteAsync(tool.Id));
            Assert.Null(await registry.GetByIdAsync(tool.Id));
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete(databasePath + "-shm");
            TryDelete(databasePath + "-wal");
        }
    }

    private static ContentSourceRegistry CreateSourceRegistry(string provider, string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ToolRegistryTest"] = connectionString,
                ["ContentStorage:AuthoringSource"] = "ToolRegistryTest",
                ["ContentStorage:Sources:ToolRegistryTest:DisplayName"] = "Tool registry test",
                ["ContentStorage:Sources:ToolRegistryTest:Provider"] = provider,
                ["ContentStorage:Sources:ToolRegistryTest:ConnectionString"] = "ToolRegistryTest"
            })
            .Build();

        return new ContentSourceRegistry(configuration, AppContext.BaseDirectory);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DatabaseToolRegistryPostgresIntegrationTests
{
    [Fact]
    public async Task PostgresRegistryRoundTripsTextArrayModes()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var sourceRegistry = CreateSourceRegistry(connectionString);
        await new ContentStorageInitializer(sourceRegistry).InitializeAsync();
        var registry = new DatabaseToolRegistry(sourceRegistry);
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"registry-postgres-{Guid.NewGuid():N}",
            DisplayName = "PostgreSQL Registry Test",
            IntegrationType = ToolIntegrationType.ProxiedApplication,
            UpstreamBaseUrl = "http://postgres-registry-test:8080",
            HealthPath = "/health",
            Modes = ["dorks-and-dice", "professional"],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await registry.SaveAsync(tool);
            var stored = await registry.GetBySlugAsync(tool.Slug.ToUpperInvariant());

            Assert.NotNull(stored);
            Assert.Equal(tool.Id, stored.Id);
            Assert.Equal(tool.IntegrationType, stored.IntegrationType);
            Assert.Equal(tool.Modes, stored.Modes);
            Assert.False(stored.AllowAnonymous);
            Assert.True(stored.Enabled);
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    private static ContentSourceRegistry CreateSourceRegistry(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ToolRegistryPostgresTest"] = connectionString,
                ["ContentStorage:AuthoringSource"] = "ToolRegistryPostgresTest",
                ["ContentStorage:Sources:ToolRegistryPostgresTest:DisplayName"] = "Tool registry PostgreSQL test",
                ["ContentStorage:Sources:ToolRegistryPostgresTest:Provider"] = "PostgreSQL",
                ["ContentStorage:Sources:ToolRegistryPostgresTest:ConnectionString"] = "ToolRegistryPostgresTest"
            })
            .Build();

        return new ContentSourceRegistry(configuration, AppContext.BaseDirectory);
    }
}
