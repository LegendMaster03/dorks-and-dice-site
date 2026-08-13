using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class ArticleNavigationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ArticleNavigationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfessionalArticleDetailLinksBackToArticles()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "http://kylebarnett.com/articles/freeing-the-bees-consolevariations-puzzle");
        request.Headers.Host = "kylebarnett.com";

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Back to articles", html);
        Assert.Contains("href=\"/articles\"", html);
        Assert.DoesNotContain("href=\"/resume#projects-section\"", html);
    }
}
