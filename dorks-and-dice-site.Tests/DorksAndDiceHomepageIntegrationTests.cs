using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class DorksAndDiceHomepageIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public DorksAndDiceHomepageIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RootUsesDatabaseBackedHomepageAfterCompiledFallbackRetirement()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://dorks-and-dice.com/");
        request.Headers.Host = "dorks-and-dice.com";

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fixture database-backed Dorks &amp; Dice homepage.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"community\"", html, StringComparison.Ordinal);
    }
}
