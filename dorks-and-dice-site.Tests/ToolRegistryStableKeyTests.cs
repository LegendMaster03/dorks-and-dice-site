using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace dorks_and_dice_site.Tests;

public sealed class ToolRegistryStableKeyTests
{
    [Fact]
    public async Task DatabaseRegistrySavesNewRegistrationNormally()
    {
        await WithDatabaseRegistryAsync(async registry =>
        {
            var registration = Service("new-database-service");

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("new-database-service", stored.Key);
            Assert.Equal(ToolKind.Service, stored.Kind);
            Assert.Null(stored.Slug);
        });
    }

    [Fact]
    public async Task JsonRegistrySavesNewRegistrationNormally()
    {
        await WithJsonRegistryAsync(async registry =>
        {
            var registration = Service("new-json-service");

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("new-json-service", stored.Key);
            Assert.Equal(ToolKind.Service, stored.Kind);
            Assert.Null(stored.Slug);
        });
    }

    [Fact]
    public async Task DatabaseRegistryUpdatesExistingRegistrationWhenStableKeyIsUnchanged()
    {
        await WithDatabaseRegistryAsync(async registry =>
        {
            var registration = Application("database-update", "database-update-route");
            await registry.SaveAsync(registration);

            registration.DisplayName = "Updated database registration";
            registration.Description = "Updated description";
            registration.HealthPath = "/healthz";
            registration.Enabled = false;
            registration.UpdatedAt = registration.UpdatedAt.AddMinutes(1);

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("database-update", stored.Key);
            Assert.Equal("Updated database registration", stored.DisplayName);
            Assert.Equal("Updated description", stored.Description);
            Assert.Equal("/healthz", stored.HealthPath);
            Assert.False(stored.Enabled);
        });
    }

    [Fact]
    public async Task DatabaseRegistryRejectsChangingExistingStableKey()
    {
        await WithDatabaseRegistryAsync(async registry =>
        {
            var registration = Application("database-stable", "database-stable-route");
            await registry.SaveAsync(registration);
            registration.Key = "database-renamed";

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => registry.SaveAsync(registration));

            Assert.Contains("can not be changed", exception.Message, StringComparison.Ordinal);
            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("database-stable", stored.Key);
        });
    }

    [Fact]
    public async Task JsonRegistryUpdatesExistingRegistrationWhenStableKeyIsUnchanged()
    {
        await WithJsonRegistryAsync(async registry =>
        {
            var registration = Application("json-update", "json-update-route");
            await registry.SaveAsync(registration);

            registration.DisplayName = "Updated JSON registration";
            registration.Description = "Updated description";
            registration.HealthPath = "/healthz";
            registration.Enabled = false;
            registration.UpdatedAt = registration.UpdatedAt.AddMinutes(1);

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("json-update", stored.Key);
            Assert.Equal("Updated JSON registration", stored.DisplayName);
            Assert.Equal("Updated description", stored.Description);
            Assert.Equal("/healthz", stored.HealthPath);
            Assert.False(stored.Enabled);
        });
    }

    [Fact]
    public async Task JsonRegistryRejectsChangingExistingStableKey()
    {
        await WithJsonRegistryAsync(async registry =>
        {
            var registration = Application("json-stable", "json-stable-route");
            await registry.SaveAsync(registration);
            registration.Key = "json-renamed";

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => registry.SaveAsync(registration));

            Assert.Contains("can not be changed", exception.Message, StringComparison.Ordinal);
            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("json-stable", stored.Key);
        });
    }

    [Fact]
    public async Task DatabaseRegistryAllowsApplicationSlugChangeWhenStableKeyIsUnchanged()
    {
        await WithDatabaseRegistryAsync(async registry =>
        {
            var registration = Application("database-routing-identity", "database-old-route");
            await registry.SaveAsync(registration);
            registration.Slug = "database-new-route";

            await registry.SaveAsync(registration);

            Assert.Null(await registry.GetBySlugAsync("database-old-route"));
            var stored = Assert.IsType<ToolRegistration>(await registry.GetBySlugAsync("database-new-route"));
            Assert.Equal(registration.Id, stored.Id);
            Assert.Equal("database-routing-identity", stored.Key);
        });
    }

    [Fact]
    public async Task JsonRegistryAllowsApplicationSlugChangeWhenStableKeyIsUnchanged()
    {
        await WithJsonRegistryAsync(async registry =>
        {
            var registration = Application("json-routing-identity", "json-old-route");
            await registry.SaveAsync(registration);
            registration.Slug = "json-new-route";

            await registry.SaveAsync(registration);

            Assert.Null(await registry.GetBySlugAsync("json-old-route"));
            var stored = Assert.IsType<ToolRegistration>(await registry.GetBySlugAsync("json-new-route"));
            Assert.Equal(registration.Id, stored.Id);
            Assert.Equal("json-routing-identity", stored.Key);
        });
    }

    [Fact]
    public async Task DatabaseRegistryDoesNotRejectCaseOnlyStableKeyDifferenceAfterNormalization()
    {
        await WithDatabaseRegistryAsync(async registry =>
        {
            var registration = Application("database-case-key", "database-case-route");
            await registry.SaveAsync(registration);
            registration.Key = "  DATABASE-CASE-KEY  ";
            registration.DisplayName = "Case-normalized database update";

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("database-case-key", stored.Key);
            Assert.Equal("Case-normalized database update", stored.DisplayName);
        });
    }

    [Fact]
    public async Task JsonRegistryDoesNotRejectCaseOnlyStableKeyDifferenceAfterNormalization()
    {
        await WithJsonRegistryAsync(async registry =>
        {
            var registration = Application("json-case-key", "json-case-route");
            await registry.SaveAsync(registration);
            registration.Key = "  JSON-CASE-KEY  ";
            registration.DisplayName = "Case-normalized JSON update";

            await registry.SaveAsync(registration);

            var stored = Assert.IsType<ToolRegistration>(await registry.GetByIdAsync(registration.Id));
            Assert.Equal("json-case-key", stored.Key);
            Assert.Equal("Case-normalized JSON update", stored.DisplayName);
        });
    }

    private static ToolRegistration Application(string key, string slug) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Kind = ToolKind.Application,
        Slug = slug,
        DisplayName = $"Application {key}",
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = "http://localhost:8123",
        FrontendEntryPoint = "/app.js",
        HealthPath = "/ready",
        Modes = ["dorks-and-dice"],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static ToolRegistration Service(string key) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Kind = ToolKind.Service,
        Slug = null,
        DisplayName = $"Service {key}",
        IntegrationType = null,
        IntegrationContractVersion = null,
        UpstreamBaseUrl = "http://localhost:8124",
        FrontendEntryPoint = null,
        HealthPath = "/ready",
        Modes = ["dorks-and-dice"],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task WithDatabaseRegistryAsync(Func<DatabaseToolRegistry, Task> action)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"tool-registry-stable-key-{Guid.NewGuid():N}.db");
        try
        {
            var sourceRegistry = CreateSourceRegistry(databasePath);
            await new ContentStorageInitializer(sourceRegistry).InitializeAsync();
            await action(new DatabaseToolRegistry(sourceRegistry));
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete(databasePath + "-shm");
            TryDelete(databasePath + "-wal");
        }
    }

    private static ContentSourceRegistry CreateSourceRegistry(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:StableKeyTests"] = $"Data Source={databasePath};Pooling=False",
                ["ContentStorage:AuthoringSource"] = "StableKeyTests",
                ["ContentStorage:Sources:StableKeyTests:DisplayName"] = "Stable key tests",
                ["ContentStorage:Sources:StableKeyTests:Provider"] = "Sqlite",
                ["ContentStorage:Sources:StableKeyTests:ConnectionString"] = "StableKeyTests"
            })
            .Build();
        return new ContentSourceRegistry(configuration, AppContext.BaseDirectory);
    }

    private static async Task WithJsonRegistryAsync(Func<JsonToolRegistry, Task> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"tool-registry-json-stable-key-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var registryPath = Path.Combine(directory, "tool-registry.json");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [JsonToolRegistry.RegistryPathConfigurationKey] = registryPath
                })
                .Build();
            var environment = new TestWebHostEnvironment(directory);
            await action(new JsonToolRegistry(environment, configuration));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "dorks-and-dice-site.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
