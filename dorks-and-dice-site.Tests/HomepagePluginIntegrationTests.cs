using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class HomepagePluginIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public HomepagePluginIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfessionalDbHomepageRendersPluginContentCollections()
    {
        var response = await SendAsync("kylebarnett.com", "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Professional Fixture", html, StringComparison.Ordinal);
        Assert.Contains("Safe Future Foundation - Full-Stack Developer", html, StringComparison.Ordinal);
        Assert.Contains("Cybersecurity Team", html, StringComparison.Ordinal);
        Assert.Contains("Xngine", html, StringComparison.Ordinal);
        Assert.Contains("Search projects or tags", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{content-collection", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DorksDbHomepageRendersInstalledDiscordPlugin()
    {
        var response = await SendAsync("dorks-and-dice.com", "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dorks &amp; Dice Fixture", html, StringComparison.Ordinal);
        Assert.Contains("Dorks &amp; Dice Discord Server", html, StringComparison.Ordinal);
        Assert.Contains("https://discord.com/widget?id=123456789&amp;theme=dark", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{discord-widget}}", html, StringComparison.Ordinal);
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
