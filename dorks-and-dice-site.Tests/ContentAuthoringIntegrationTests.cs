using System.Net;
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
    public async Task ContentEditorIsAvailableOnDevelopmentHost()
    {
        var response = await SendAsync("localhost", "/development/content");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Content Authoring", html);
        Assert.Contains("New draft", html);
        Assert.Contains("Local authoring workspace", html);
    }

    [Fact]
    public async Task ContentEditorIsNotAvailableOnProductionHost()
    {
        var response = await SendAsync("kylebarnett.com", "/development/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GlobalSourceCanCreateAnArticle()
    {
        var response = await SendAsync("localhost", "/development/content/new?source=External");
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
        var get = new HttpRequestMessage(HttpMethod.Get, "http://localhost/development/content/new?source=Local");
        get.Headers.Host = "localhost";
        var page = await client.SendAsync(get);
        var pageHtml = await page.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(
            pageHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")
            .Groups["token"].Value;
        Assert.NotEmpty(token);

        var post = new HttpRequestMessage(HttpMethod.Post, "http://localhost/development/content/visual/render");
        post.Headers.Host = "localhost";
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
