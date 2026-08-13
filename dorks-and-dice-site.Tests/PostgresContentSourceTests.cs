using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dorks_and_dice_site.Tests;

public sealed class PostgresContentSourceTests
{
    [Fact]
    public void LocalContentDatabaseIsExcludedFromPublishedApplication()
    {
        var projectDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dorks-and-dice-site"));
        var project = System.Xml.Linq.XDocument.Load(
            Path.Combine(projectDirectory, "dorks-and-dice-site.csproj"));
        var databaseContent = Assert.Single(project
            .Descendants("Content")
            .Where(element => string.Equals(
                element.Attribute("Update")?.Value,
                @"Content\**\*.db",
                StringComparison.Ordinal)));

        Assert.Equal("PreserveNewest", databaseContent.Element("CopyToOutputDirectory")?.Value);
        Assert.Equal("Never", databaseContent.Element("CopyToPublishDirectory")?.Value);
    }

    [Fact]
    public void ProductionConfigurationDoesNotExposeLocalContentSource()
    {
        var projectDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dorks-and-dice-site"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(projectDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(projectDirectory, "appsettings.Production.json"))
            .Build();

        var sourceKeys = configuration.GetSection("ContentStorage:Sources")
            .GetChildren()
            .Select(section => section.Key)
            .ToList();

        Assert.Contains("External", sourceKeys);
        Assert.DoesNotContain("Local", sourceKeys);
        Assert.Equal("External", configuration["ContentStorage:AuthoringSource"]);
    }

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

    [Fact]
    public void RegistryBuildsPostgresConnectionStringFromSecretFileWithoutManualEscaping()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"postgres-secret-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var passwordFile = Path.Combine(directory, "password");
        const string password = "te;st=p#ass\"word";
        File.WriteAllText(passwordFile, password + Environment.NewLine);

        try
        {
            var settings = new Dictionary<string, string?>
            {
                ["ContentStorage:AuthoringSource"] = "External",
                ["ContentStorage:Sources:External:Provider"] = "PostgreSQL",
                ["ContentStorage:Sources:External:Host"] = "postgres",
                ["ContentStorage:Sources:External:Port"] = "5432",
                ["ContentStorage:Sources:External:Database"] = "dorks_and_dice_content",
                ["ContentStorage:Sources:External:Username"] = "dorks_and_dice_site",
                ["ContentStorage:Sources:External:PasswordFile"] = passwordFile
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());
            var parsed = new NpgsqlConnectionStringBuilder(registry.GetSource("External").ConnectionString);

            Assert.Equal("postgres", parsed.Host);
            Assert.Equal(5432, parsed.Port);
            Assert.Equal("dorks_and_dice_content", parsed.Database);
            Assert.Equal("dorks_and_dice_site", parsed.Username);
            Assert.Equal(password, parsed.Password);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
