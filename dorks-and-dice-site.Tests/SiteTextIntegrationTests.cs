using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class SiteTextIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public SiteTextIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SiteTextUsesThePublicProfessionalCatalog()
    {
        var response = await SendAsync("/site.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", text);
        Assert.Contains("http://kylebarnett.com/articles/freeing-the-bees-consolevariations-puzzle", text);
        Assert.Contains("Personal Multi-Mode Website", text);
        Assert.DoesNotContain("_internal:unlisted", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/development", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmsTextProvidesACompactPublicIndex()
    {
        var response = await SendAsync("/llms.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Full public text: http://kylebarnett.com/site.txt", text);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", text);
        Assert.DoesNotContain("## Freeing the Bees\n\n- Inspect the console", text);
    }

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://kylebarnett.com{path}");
        request.Headers.Host = "kylebarnett.com";
        return await client.SendAsync(request);
    }
}
