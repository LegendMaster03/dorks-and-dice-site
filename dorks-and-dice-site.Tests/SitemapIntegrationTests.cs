using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class SitemapIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public SitemapIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SitemapIncludesCurrentDatabaseBackedPublicContent()
    {
        var response = await SendAsync("/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("http://kylebarnett.com/articles/freeing-the-bees-consolevariations-puzzle", xml);
        Assert.Contains("http://kylebarnett.com/resume/personalmultimodewebsite", xml);
        Assert.Contains("http://kylebarnett.com/resume/experiencecybersecurityteam?context=experience", xml);
        Assert.DoesNotContain("/development", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SitemapKeepsIntrinsicModeRoutesAlongsideDatabaseContent()
    {
        var response = await SendAsync("/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<loc>http://kylebarnett.com/</loc>", xml);
        Assert.Contains("<loc>http://kylebarnett.com/articles</loc>", xml);
    }

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://kylebarnett.com{path}");
        request.Headers.Host = "kylebarnett.com";
        return await client.SendAsync(request);
    }
}
