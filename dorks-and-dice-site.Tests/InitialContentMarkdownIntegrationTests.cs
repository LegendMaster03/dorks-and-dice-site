using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class InitialContentMarkdownIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InitialContentMarkdownIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/resume/xngine", "<h2", "<ul>")]
    [InlineData("/resume/personalmultimodewebsite", "<h2", "<table")]
    [InlineData("/articles/freeing-the-bees-consolevariations-puzzle", "<h2", "<blockquote>")]
    public async Task MigratedMarkdownRendersAsStructuredHtml(string path, string firstElement, string secondElement)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost{path}");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional; DevelopmentEnabledContentSources=Local");

        var response = await client.SendAsync(request);
        var rendered = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(firstElement, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondElement, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&lt;h2", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&lt;p", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">&lt;h2", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">&lt;p", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitialMarkdownImageDestinationsUseManagedMediaUrls()
    {
        var databasePath = GetDatabasePath();
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT revision_body FROM content_revision";
        using var reader = command.ExecuteReader();

        var imageCount = 0;
        while (reader.Read())
        {
            var body = reader.GetString(0);
            var destinations = Regex.Matches(body, @"!\[[^\]]*\]\((?<url>[^\s)]+)");
            imageCount += destinations.Count;
            Assert.All(destinations, match =>
                Assert.True(match.Groups["url"].Value.StartsWith("/content/media/", StringComparison.Ordinal)));
        }

        Assert.Equal(13, imageCount);
    }

    [Fact]
    public async Task SkyblivionRendersManagedMediaAsTheExistingGallery()
    {
        var rendered = await GetLocalContentAsync("/resume/skyblivion");
        var expectedImages = new[]
        {
            "agarmirs-house.jpg", "dorians-house.jpg", "timberscar-hollow.jpg",
            "seridurs-house-basement.jpg", "bruma-player-home.jpg"
        };

        foreach (var image in expectedImages)
        {
            Assert.Matches(
                $"src=\"/content/media/[0-9a-f]{{32}}/{Regex.Escape(image)}\"",
                rendered);
        }

        Assert.Contains("class=\"project-gallery\"", rendered, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(rendered, "<figure(?:[ >])", RegexOptions.IgnoreCase).Count);
        Assert.Equal(5, Regex.Matches(rendered, "<figcaption(?:[ >])", RegexOptions.IgnoreCase).Count);
        Assert.Equal(5, Regex.Matches(rendered, "class=\"card-img-top\"", RegexOptions.IgnoreCase).Count);
    }

    [Fact]
    public async Task ArticleEndingRemainsBehindItsSpoilerDisclosure()
    {
        var rendered = await GetLocalContentAsync("/articles/freeing-the-bees-consolevariations-puzzle");

        Assert.Contains("<details class=\"content-spoiler border rounded p-3 mb-3\">", rendered, StringComparison.Ordinal);
        Assert.Contains("<summary class=\"fw-semibold\">Reveal the ending</summary>", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Reveal the ending</h2>", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"alert alert-warning mt-4\" role=\"alert\"", rendered, StringComparison.Ordinal);
        Assert.Contains("class=\"content-note alert alert-secondary\" role=\"note\"", rendered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/resume/directedindependentstudy", "/resume/experiencecybersecurityteam?context=experience")]
    [InlineData("/resume/experiencecybersecurityteam", "/resume/directedindependentstudy")]
    [InlineData("/resume/experiencesimlab", "/resume/simlabexpo")]
    public async Task ConvertedInternalLinksRetainTheirDestinations(string path, string destination)
    {
        var rendered = await GetLocalContentAsync(path);

        Assert.Contains($"href=\"{destination}", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"\"", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertedFiguresRetainCaptionsDimensionsAndPresentationClasses()
    {
        var rendered = await GetLocalContentAsync("/resume/seniorproject");

        Assert.Equal(3, Regex.Matches(rendered, "<figure(?:[ >])", RegexOptions.IgnoreCase).Count);
        Assert.Equal(3, Regex.Matches(rendered, "<figcaption(?:[ >])", RegexOptions.IgnoreCase).Count);
        Assert.Contains("width=\"560\" height=\"560\"", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"img-fluid rounded border d-block mx-auto\"", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkdownContentImagesHaveResponsiveApplicationCss()
    {
        var client = _factory.CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        Assert.Matches(@"\.content-detail-body\s+img\s*\{[^}]*max-width:\s*100%;[^}]*height:\s*auto;", css);
        Assert.Matches(@"\.content-detail-body\s+h2\s*\{[^}]*font-size:\s*1\.25rem;", css);
        Assert.Matches(@"\.content-detail-body\s+table\s*\{[^}]*width:\s*100%;", css);
    }

    private static string GetDatabasePath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "dorks-and-dice-site", "Content", "content.db"));

    private async Task<string> GetLocalContentAsync(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost{path}");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional; DevelopmentEnabledContentSources=Local");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
