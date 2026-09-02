using System.Net;
using dorks_and_dice_site.Services.Identity;
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
    public async Task TrustedAnonymousPreviewOnlyShowsModeSwitch()
    {
        var response = await SendAsync("localhost", "/articles", includeDeveloperRole: false);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Trusted Preview", html);
        Assert.Contains(">Mode<", html);
        Assert.DoesNotContain("Show unlisted articles", html);
        Assert.DoesNotContain("Content editor", html);
        Assert.DoesNotContain("Database sources", html);
    }

    [Fact]
    public async Task DeveloperArticlesMenuListsAllConfiguredSources()
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
    public async Task DeveloperCanDisableAllContentSourcesExplicitly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var post = new HttpRequestMessage(HttpMethod.Post, "https://localhost/development-preview")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["articleSettings"] = "true",
                ["returnUrl"] = "/articles"
            })
        };
        post.Headers.Host = "localhost";
        post.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        var settingsResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);
        var sourceCookie = settingsResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("DevelopmentEnabledContentSources=", StringComparison.Ordinal));
        var cookiePair = sourceCookie.Split(';', 2)[0];
        Assert.Contains("DevelopmentEnabledContentSources=__none__", cookiePair);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/articles");
        request.Headers.Host = "localhost";
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        request.Headers.Add("Cookie", cookiePair);
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Freeing the Bees: Solving ConsoleVariations", html);
    }

    [Fact]
    public async Task DeveloperCanSelectExternalSourceIndependently()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var post = new HttpRequestMessage(HttpMethod.Post, "https://localhost/development-preview")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["articleSettings"] = "true",
                ["returnUrl"] = "/articles",
                ["enabledContentSource"] = "External"
            })
        };
        post.Headers.Host = "localhost";
        post.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        var settingsResponse = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);
        var sourceCookie = settingsResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("DevelopmentEnabledContentSources=", StringComparison.Ordinal));
        var cookiePair = sourceCookie.Split(';', 2)[0];
        Assert.Contains("DevelopmentEnabledContentSources=External", cookiePair);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/articles");
        request.Headers.Host = "localhost";
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        request.Headers.Add("Cookie", cookiePair);
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", html);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string host,
        string path,
        bool includeDeveloperRole = true)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{path}");
        request.Headers.Host = host;
        if (includeDeveloperRole)
        {
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        }
        return await client.SendAsync(request);
    }
}
