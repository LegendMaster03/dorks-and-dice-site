using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed partial class PublishedContentSmokeTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public PublishedContentSmokeTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/articles/freeing-the-bees-consolevariations-puzzle")]
    [InlineData("/resume/personalmultimodewebsite")]
    [InlineData("/resume/dndtools")]
    [InlineData("/resume/skyblivion")]
    [InlineData("/resume/pythonfinanceanalytics")]
    [InlineData("/resume/xngine")]
    [InlineData("/resume/experiencecybersecurityteam")]
    [InlineData("/resume/seniorproject")]
    public async Task PublishedDetailPagesRenderWithoutLiteralLegacyMarkup(string path)
    {
        var response = await SendAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("&lt;h2", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&lt;p", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("~/", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryManagedImageRenderedByPublishedPagesIsRetrievable()
    {
        var paths = new[]
        {
            "/articles/freeing-the-bees-consolevariations-puzzle",
            "/resume/personalmultimodewebsite",
            "/resume/dndtools",
            "/resume/skyblivion",
            "/resume/pythonfinanceanalytics",
            "/resume/xngine",
            "/resume/experiencecybersecurityteam",
            "/resume/seniorproject"
        };
        var mediaUrls = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var page = await SendAsync(path);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var html = await page.Content.ReadAsStringAsync();
            foreach (Match match in ManagedImagePattern().Matches(html))
            {
                mediaUrls.Add(WebUtility.HtmlDecode(match.Groups["url"].Value));
            }
        }

        Assert.NotEmpty(mediaUrls);
        foreach (var mediaUrl in mediaUrls)
        {
            var media = await SendAsync(mediaUrl);
            Assert.Equal(HttpStatusCode.OK, media.StatusCode);
            Assert.StartsWith("image/", media.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await media.Content.ReadAsByteArrayAsync());
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://kylebarnett.com{path}");
        request.Headers.Host = "kylebarnett.com";
        return await client.SendAsync(request);
    }

    [GeneratedRegex("<img[^>]+src=\\\"(?<url>/content/media/[^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex ManagedImagePattern();
}
