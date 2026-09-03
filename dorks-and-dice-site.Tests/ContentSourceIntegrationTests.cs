using System.Net;
using dorks_and_dice_site.Models.Identity;
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
        var response = await SendAsync("localhost", "/articles");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Trusted Preview", html);
        Assert.Contains(">Mode<", html);
        Assert.DoesNotContain("Preview settings", html);
        Assert.DoesNotContain("Show unlisted articles", html);
        Assert.DoesNotContain("Database sources", html);
    }

    [Fact]
    public async Task DeveloperPreviewSettingsOnlyExposeDatabaseSources()
    {
        var response = await SendAsync("localhost", "/articles", roles: AccountRoles.Dev);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Preview settings", html);
        Assert.DoesNotContain("Show unlisted articles", html);
        Assert.Contains("External content", html);
        Assert.Contains("Local content", html);
        Assert.Contains("Database sources", html);
        Assert.DoesNotContain("Content editor", html);
    }

    [Fact]
    public async Task ScopedEditorPreviewSettingsOnlyExposeUnlistedContentForAssignedMode()
    {
        var professionalEditor = $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}";
        var response = await SendAsync(
            "localhost",
            "/articles",
            scopedRoles: professionalEditor,
            cookie: "DevelopmentPreviewSiteMode=professional");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Preview settings", html);
        Assert.Contains("Show unlisted articles", html);
        Assert.DoesNotContain("Database sources", html);

        var wrongModeResponse = await SendAsync(
            "localhost",
            "/articles",
            scopedRoles: professionalEditor,
            cookie: "DevelopmentPreviewSiteMode=dorks-and-dice");
        var wrongModeHtml = await wrongModeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, wrongModeResponse.StatusCode);
        Assert.DoesNotContain("Preview settings", wrongModeHtml);
        Assert.DoesNotContain("Show unlisted articles", wrongModeHtml);
    }

    [Fact]
    public async Task AdminPreviewSettingsExposeUnlistedContentWithoutDeveloperSources()
    {
        var response = await SendAsync(
            "localhost",
            "/articles",
            roles: AccountRoles.Admin,
            cookie: "DevelopmentPreviewSiteMode=dorks-and-dice");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Show unlisted articles", html);
        Assert.DoesNotContain("Database sources", html);
    }

    [Fact]
    public async Task EditorCanToggleUnlistedContentPreview()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var post = new HttpRequestMessage(HttpMethod.Post, "https://localhost/development-preview")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["editorPreviewSettings"] = "true",
                ["includeUnlisted"] = "true",
                ["returnUrl"] = "/articles"
            })
        };
        post.Headers.Host = "localhost";
        post.Headers.Add(
            TestRoleAuthenticationHandler.ScopedRolesHeader,
            $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");
        post.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional");

        var response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("DevelopmentIncludeUnlistedArticles=true", StringComparison.Ordinal));
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
                ["developerPreviewSettings"] = "true",
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
                ["developerPreviewSettings"] = "true",
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
        string? roles = null,
        string? scopedRoles = null,
        string? cookie = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}{path}");
        request.Headers.Host = host;
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, roles);
        }
        if (!string.IsNullOrWhiteSpace(scopedRoles))
        {
            request.Headers.Add(TestRoleAuthenticationHandler.ScopedRolesHeader, scopedRoles);
        }
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }
        return await client.SendAsync(request);
    }
}
