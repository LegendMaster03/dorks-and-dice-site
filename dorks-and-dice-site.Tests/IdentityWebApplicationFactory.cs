using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _identityConnectionString;

    public IdentityWebApplicationFactory(string identityConnectionString)
    {
        _identityConnectionString = identityConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var contentConnectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(contentConnectionString))
        {
            throw new InvalidOperationException("CONTENT_TEST_POSTGRES is required for identity integration tests.");
        }

        builder.UseSetting("ConnectionStrings:IdentityDatabase", _identityConnectionString);
        builder.UseSetting("IdentityStorage:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("ConnectionStrings:IdentityTestContent", contentConnectionString);
        builder.UseSetting("ContentStorage:AuthoringSource", "External");
        builder.UseSetting("ContentStorage:Sources:External:DisplayName", "Test content");
        builder.UseSetting("ContentStorage:Sources:External:Provider", "PostgreSQL");
        builder.UseSetting("ContentStorage:Sources:External:ConnectionString", "IdentityTestContent");
        builder.UseSetting("ContentStorage:GlobalSources:0", "External");
    }
}
