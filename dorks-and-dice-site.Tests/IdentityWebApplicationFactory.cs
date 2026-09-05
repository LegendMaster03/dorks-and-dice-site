using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

public sealed class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _identityConnectionString;
    private readonly string _toolDirectory = Path.Combine(
        Path.GetTempPath(), $"dorks-and-dice-identity-tools-{Guid.NewGuid():N}");

    public IdentityWebApplicationFactory(string identityConnectionString)
    {
        _identityConnectionString = identityConnectionString;
    }

    public TestAccountEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var contentConnectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(contentConnectionString))
        {
            throw new InvalidOperationException("CONTENT_TEST_POSTGRES is required for identity integration tests.");
        }

        // Avoid appsettings.Development.json adding the Local SQLite source. Identity integration
        // tests intentionally exercise both content initialization and Identity against PostgreSQL.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ToolHosting:RegistryPath", Path.Combine(_toolDirectory, "tool-registry.json"));
        builder.UseSetting("CampaignStorage:Path", Path.Combine(_toolDirectory, "campaign-access.json"));
        builder.UseSetting("ConnectionStrings:IdentityDatabase", _identityConnectionString);
        builder.UseSetting("IdentityStorage:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("ConnectionStrings:IdentityTestContent", contentConnectionString);
        builder.UseSetting("ContentStorage:AuthoringSource", "External");
        builder.UseSetting("ContentStorage:Sources:External:DisplayName", "Test content");
        builder.UseSetting("ContentStorage:Sources:External:Provider", "PostgreSQL");
        builder.UseSetting("ContentStorage:Sources:External:ConnectionString", "IdentityTestContent");
        builder.UseSetting("ContentStorage:GlobalSources:0", "External");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(EmailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_toolDirectory))
            Directory.Delete(_toolDirectory, recursive: true);
    }
}
