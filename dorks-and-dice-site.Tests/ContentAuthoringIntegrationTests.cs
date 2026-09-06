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
        Assert.Contains("Professional Content Authoring", html);
        Assert.Contains("New content", html);
        Assert.DoesNotContain("Content database", html);
        Assert.DoesNotContain("Push all", html);
    }

    [Fact]
    public async Task EditorEntryRoutesDirectlyToTheAvailableAuthoringSurface()
    {
        var modeEditor = await SendAsync(
            "kylebarnett.com",
            "/editor",
            scopedRoles: $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");
        Assert.Equal(HttpStatusCode.Redirect, modeEditor.StatusCode);
        Assert.Equal("/editor/content", modeEditor.Headers.Location?.OriginalString);

        var developer = await SendAsync("localhost", "/editor", roles: AccountRoles.Dev);
        Assert.Equal(HttpStatusCode.Redirect, developer.StatusCode);
        Assert.Equal("/development/content", developer.Headers.Location?.OriginalString);
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
    public async Task AdminInheritanceGrantsModeEditorOnPublicHost()
    {
        var response = await SendAsync("kylebarnett.com", "/editor/content", roles: AccountRoles.Admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeveloperRoleDoesNotImplyModeEditorRole()
    {
        var response = await SendAsync("kylebarnett.com", "/editor/content", roles: AccountRoles.Dev);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CentralContentAuthoringIsDeveloperOnlyAndTrustedOnly()
    {
        var trusted = await SendAsync("localhost", "/development/content", roles: AccountRoles.Dev);
        var trustedHtml = await trusted.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, trusted.StatusCode);
        Assert.Contains("Central Content Authoring", trustedHtml);

        var adminOnly = await SendAsync("localhost", "/development/content", roles: AccountRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, adminOnly.StatusCode);

        var publicResponse = await SendAsync("kylebarnett.com", "/development/content", roles: AccountRoles.Dev);
        Assert.Equal(HttpStatusCode.Forbidden, publicResponse.StatusCode);
    }

    [Fact]
    public async Task DevelopmentPreviewContentEditTargetsCentralAuthoringSource()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://localhost/articles/freeing-the-bees-consolevariations-puzzle");
        request.Headers.Host = "localhost";
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional");

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "/development/content/freeing-the-bees-consolevariations-puzzle/edit?source=External",
            html,
            StringComparison.Ordinal);
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
    public async Task ModeEditorPageRetainsEditorsButLocksModeAssignment()
    {
        var response = await SendAsync("kylebarnett.com", "/editor/content/new", roles: AccountRoles.Admin);
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
        Assert.Contains("Mode assignment is controlled by the active mode editor", html);
        Assert.DoesNotContain("id=\"visible-mode-dropdown\"", html);
        Assert.DoesNotContain("visible-mode-dorks-and-dice", html);
    }

    [Fact]
    public async Task CentralAuthoringRetainsCrossModeSelection()
    {
        var response = await SendAsync("localhost", "/development/content/new", roles: AccountRoles.Dev);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"visible-mode-dropdown\"", html);
        Assert.Contains("visible-mode-professional", html);
        Assert.Contains("visible-mode-dorks-and-dice", html);
        Assert.DoesNotContain("visible-mode-Development", html);
        Assert.DoesNotContain("visible-mode-Unassigned", html);
    }

    [Fact]
    public async Task VisualEditorRendersSafeMarkdownAndProtectsDirectives()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var get = new HttpRequestMessage(HttpMethod.Get, "https://kylebarnett.com/editor/content/new");
        get.Headers.Host = "kylebarnett.com";
        get.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Admin);
        var page = await client.SendAsync(get);
        var pageHtml = await page.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(
            pageHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")
            .Groups["token"].Value;
        Assert.NotEmpty(token);

        var post = new HttpRequestMessage(HttpMethod.Post, "https://kylebarnett.com/editor/content/visual/render");
        post.Headers.Host = "kylebarnett.com";
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
