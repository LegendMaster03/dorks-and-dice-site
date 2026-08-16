using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ContentSourceIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ContentSourceIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DevelopmentArticlesMenuListsAllConfiguredSources()
    {
        var response = await SendAsync("localhost", "/articles");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(">Articles<", html);
        Assert.Contains("Show unlisted articles", html);
        Assert.Contains("External content", html);
        Assert.Contains("Local content", html);
        Assert.Contains("Content editor", html);
    }

    [Fact]
    public async Task DevelopmentCanDisableAllContentSourcesExplicitly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var post = new HttpRequestMessage(HttpMethod.Post, "http://localhost/development-preview")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["articleSettings"] = "true",
                ["returnUrl"] = "/articles"
            })
        };
        post.Headers.Host = "localhost";
        var settingsResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);
        var sourceCookie = settingsResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("DevelopmentEnabledContentSources=", StringComparison.Ordinal));
        var cookiePair = sourceCookie.Split(';', 2)[0];
        Assert.Contains("DevelopmentEnabledContentSources=__none__", cookiePair);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/articles");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", cookiePair);
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Freeing the Bees: Solving ConsoleVariations", html);
    }

    [Fact]
    public async Task DevelopmentCanSelectExternalSourceIndependently()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var post = new HttpRequestMessage(HttpMethod.Post, "http://localhost/development-preview")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["articleSettings"] = "true",
                ["returnUrl"] = "/articles",
                ["enabledContentSource"] = "External"
            })
        };
        post.Headers.Host = "localhost";
        var settingsResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);
        var sourceCookie = settingsResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("DevelopmentEnabledContentSources=", StringComparison.Ordinal));
        var cookiePair = sourceCookie.Split(';', 2)[0];
        Assert.Contains("DevelopmentEnabledContentSources=External", cookiePair);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/articles");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", cookiePair);
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", html);
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
