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
    public void InitialMarkdownImageDestinationsAreRootRelative()
    {
        var databasePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "dorks-and-dice-site", "Content", "content.db"));
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
            Assert.DoesNotContain(destinations, match => match.Groups["url"].Value.StartsWith("~/", StringComparison.Ordinal));
        }

        Assert.Equal(13, imageCount);
    }

    [Fact]
    public async Task SkyblivionRendersAllFiveRootRelativeGalleryImages()
    {
        var rendered = await GetLocalContentAsync("/resume/skyblivion");
        var expectedImages = new[]
        {
            "agarmirs-house.jpg", "dorians-house.jpg", "timberscar-hollow.jpg",
            "seridurs-house-basement.jpg", "bruma-player-home.jpg"
        };

        foreach (var image in expectedImages)
        {
            Assert.Contains($"src=\"/site-modes/professional/images/skyblivion/{image}\"", rendered);
        }
    }

    [Fact]
    public async Task MarkdownContentImagesHaveResponsiveApplicationCss()
    {
        var client = _factory.CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        Assert.Matches(@"\.content-detail-body\s+img\s*\{[^}]*max-width:\s*100%;[^}]*height:\s*auto;", css);
    }

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
