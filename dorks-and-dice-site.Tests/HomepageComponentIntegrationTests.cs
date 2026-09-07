using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class HomepageComponentIntegrationTests
{
    [Fact]
    public async Task ProfessionalHomepageRendersExistingExperienceAndProjectCollections()
    {
        using var factory = new PublishedContentWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://kylebarnett.com/");
        request.Headers.Host = "kylebarnett.com";
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fixture database-backed Professional homepage.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"experience-section\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe Future Foundation - Full-Stack Developer", html, StringComparison.Ordinal);
        Assert.Contains("Cybersecurity Team", html, StringComparison.Ordinal);
        Assert.Contains("id=\"projectFilters\"", html, StringComparison.Ordinal);
        Assert.Contains("Personal Multi-Mode Website", html, StringComparison.Ordinal);
    }
}
