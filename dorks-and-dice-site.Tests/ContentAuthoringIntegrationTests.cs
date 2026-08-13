using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContentAuthoringIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ContentEditorIsAvailableOnDevelopmentHost()
    {
        var response = await SendAsync("localhost", "/development/content");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Content Authoring", html);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", html);
        Assert.Contains("New content page", html);
    }

    [Fact]
    public async Task ContentEditorIsNotAvailableOnProductionHost()
    {
        var response = await SendAsync("kylebarnett.com", "/development/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingContentEditorExposesRevisionSource()
    {
        var response = await SendAsync(
            "localhost",
            "/development/content/freeing-the-bees-consolevariations-puzzle/edit");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Edit consolevariations-free-the-bees", html);
        Assert.Contains("Revision metadata", html);
        Assert.Contains("technical-investigation", html);
        Assert.Contains("Revision history", html);
    }

    private async Task<HttpResponseMessage> SendAsync(string host, string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        return await client.SendAsync(request);
    }
}
