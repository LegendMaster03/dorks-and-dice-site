using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _identityConnectionString;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"dorks-and-dice-identity-integration-{Guid.NewGuid():N}");

    public IdentityWebApplicationFactory(string identityConnectionString)
    {
        _identityConnectionString = identityConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        var contentPath = Path.Combine(_directory, "content.db");

        builder.UseSetting("ConnectionStrings:IdentityDatabase", _identityConnectionString);
        builder.UseSetting("IdentityStorage:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("ConnectionStrings:ContentDatabaseLocal", $"Data Source={contentPath};Pooling=False");
        builder.UseSetting("ContentStorage:AuthoringSource", "Local");
        builder.UseSetting("ContentStorage:Sources:Local:DisplayName", "Test content");
        builder.UseSetting("ContentStorage:Sources:Local:Provider", "Sqlite");
        builder.UseSetting("ContentStorage:Sources:Local:ConnectionString", "ContentDatabaseLocal");
        builder.UseSetting("ContentStorage:GlobalSources:0", "Local");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
