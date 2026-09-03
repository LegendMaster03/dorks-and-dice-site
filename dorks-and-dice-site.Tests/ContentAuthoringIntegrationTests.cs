using System.Net;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ContentAuthoringIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ContentAuthoringIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ContentEditorIsAvailableToScopedEditorInMatchingMode()
    {
        var response = await SendAsync(
            "kylebarnett.com",
            "/editor/content",
            scopedRoles: $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Content Authoring", html);
        Assert.Contains("New content", html);
        Assert.DoesNotContain("Content database", html);
        Assert.DoesNotContain("Push all", html);
    }

    [Fact]
    public async Task ScopedEditorCanNotEditAnotherSiteMode()
    {
        var response = await SendAsync(
            "dorks-and-dice.com",
            "/editor/content",
            scopedRoles: $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminActsAsGlobalEditorOnTrustedHost()
    {
        var response = await SendAsync("localhost", "/editor/content", roles: AccountRoles.Admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeveloperRoleDoesNotImplyEditorRole()
    {
        var response = await SendAsync("localhost", "/editor/content", roles: AccountRoles.Dev);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ContentDatabaseManagerIsDeveloperOnlyAndTrustedOnly()
    {
        var trusted = await SendAsync("localhost", "/development/databases", roles: AccountRoles.Dev);
        var trustedHtml = await trusted.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, trusted.StatusCode);
        Assert.Contains("Content Databases", trustedHtml);

        var publicResponse = await SendAsync("kylebarnett.com", "/development/databases", roles: AccountRoles.Dev);
        Assert.Equal(HttpStatusCode.Forbidden, publicResponse.StatusCode);
    }

    [Fact]
    public async Task EditorPageRetainsVisualAndMetadataEditors()
    {
        var response = await SendAsync("localhost", "/editor/content/new", roles: AccountRoles.Admin);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("content-source-tab", html);
        Assert.Contains("content-visual-tab", html);
        Assert.Contains("content-visual-editor.js", html);
        Assert.Contains("metadata-standard-tab", html);
        Assert.Contains("metadata-source-tab", html);
        Assert.Contains("id=\"metadata-standard-editor\" class=\"card card-body d-none\"", html);
        Assert.Contains("id=\"metadata-source-editor\"", html);
        Assert.Contains("data-meta-path=\"title\"", html);
        Assert.Contains("data-meta-path=\"presentations.project.title\"", html);
        Assert.Contains("content-metadata-editor.js", html);
        Assert.Contains("visible-mode-Professional", html);
        Assert.Contains("visible-mode-DorksAndDice", html);
        Assert.DoesNotContain("visible-mode-Development", html);
        Assert.DoesNotContain("visible-mode-Unassigned", html);
    }

    [Fact]
    public async Task VisualEditorRendersSafeMarkdownAndProtectsDirectives()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var get = new HttpRequestMessage(HttpMethod.Get, "https://localhost/editor/content/new");
        get.Headers.Host = "localhost";
        get.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Admin);
        var page = await client.SendAsync(get);
        var pageHtml = await page.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(
            pageHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")
            .Groups["token"].Value;
        Assert.NotEmpty(token);

        var post = new HttpRequestMessage(HttpMethod.Post, "https://localhost/editor/content/visual/render");
        post.Headers.Host = "localhost";
        post.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Admin);
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["body"] = "## Heading\n\n- One\n- Two\n\n{{resume-architecture}}"
        });
        var response = await client.SendAsync(post);
        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var html = document.RootElement.GetProperty("html").GetString()!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h2", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("content-visual-directive", html);
        Assert.Contains("{{resume-architecture}}", html);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string host,
        string path,
        string? roles = null,
        string? scopedRoles = null)
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
        return await client.SendAsync(request);
    }
}
