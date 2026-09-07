using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ProfessionalHomepageIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ProfessionalHomepageIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/resume")]
    public async Task ProfessionalHomeRoutesUseDatabaseBackedHomepageAfterCompiledFallbackRetirement(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://kylebarnett.com{path}");
        request.Headers.Host = "kylebarnett.com";

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fixture database-backed Professional homepage.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"experience-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"projects-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"skills-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"education-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"honors-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"leadership-section\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"projectList\"", html, StringComparison.Ordinal);
    }
}
