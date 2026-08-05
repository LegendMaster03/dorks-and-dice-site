using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SiteModeIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("kylebarnett.com", "/", HttpStatusCode.OK)]
    [InlineData("kylebarnett.com", "/resume", HttpStatusCode.OK)]
    [InlineData("kylebarnett.com", "/site-modes/dorks-and-dice/images/sample.png", HttpStatusCode.NotFound)]
    [InlineData("dorks-and-dice.com", "/", HttpStatusCode.OK)]
    [InlineData("dorks-and-dice.com", "/resume", HttpStatusCode.NotFound)]
    [InlineData("dorks-and-dice.com", "/site-modes/professional/files/kyle-resume.pdf", HttpStatusCode.NotFound)]
    [InlineData("unassigned.example", "/", HttpStatusCode.OK)]
    [InlineData("unassigned.example", "/resume", HttpStatusCode.NotFound)]
    [InlineData("unassigned.example", "/site-modes/professional/images/profile/kyle-headshot.jpg", HttpStatusCode.NotFound)]
    public async Task HostModeRouteMatrixReturnsExpectedStatus(string host, string path, HttpStatusCode expectedStatusCode)
    {
        var response = await SendAsync(host, path);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task GeneratedScopedStylesheetIsAllowedAsSharedStaticAsset()
    {
        var response = await SendAsync("kylebarnett.com", "/dorks-and-dice-site.styles.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("k-barnett.com")]
    [InlineData("www.kylebarnett.com")]
    public async Task ProfessionalAliasRedirectsToCanonicalDomain(string host)
    {
        var response = await SendAsync(host, "/resume?ref=test");

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal("https://kylebarnett.com/resume?ref=test", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ForwardedProtoIsUsedForCanonicalMetadata()
    {
        using var request = CreateRequest("kylebarnett.com", "/resume");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("""<link rel="canonical" href="https://kylebarnett.com/resume" />""", html);
        Assert.Contains("""<meta property="og:url" content="https://kylebarnett.com/resume" />""", html);
    }

    [Fact]
    public async Task ProfessionalArticlesIndexDoesNotListUnlistedArticle()
    {
        var response = await SendAsync("kylebarnett.com", "/articles");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Freeing the Bees: Solving ConsoleVariations", html);
    }

    [Fact]
    public async Task ProfessionalDirectArticleAccessReturnsNoindex()
    {
        var response = await SendAsync("kylebarnett.com", "/articles/freeing-the-bees-consolevariations-puzzle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("""<meta name="robots" content="noindex, nofollow" />""", html);
    }

    [Theory]
    [InlineData("/Resume/XnGine", "Back to projects", "/resume#projects-section")]
    [InlineData("/Resume/ExperienceCyberSecurityTeam", "Back to experience", "/resume#experience-section")]
    public async Task ProfessionalDetailPagesIncludeBackLink(string path, string linkText, string href)
    {
        var response = await SendAsync("kylebarnett.com", path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(linkText, html);
        Assert.Contains($"href=\"{href}\"", html);
    }

    [Fact]
    public async Task DorksCannotAccessProfessionalOnlyArticle()
    {
        var response = await SendAsync("dorks-and-dice.com", "/articles/freeing-the-bees-consolevariations-puzzle");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentPreviewCanRenderRestrictedRouteWithWarning()
    {
        using var request = CreateRequest("localhost", "/resume");
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=dorks-and-dice");

        var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This page is not available in the selected site mode.", html);
        Assert.Contains("Dorks &amp; Dice", html);
    }

    [Fact]
    public async Task DevelopmentPreviewCanListUnlistedArticles()
    {
        using var request = CreateRequest("localhost", "/articles");
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=development; DevelopmentIncludeUnlistedArticles=true");

        var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", html);
        Assert.Contains("Unlisted", html);
    }

    private async Task<HttpResponseMessage> SendAsync(string host, string path)
    {
        using var request = CreateRequest(host, path);
        return await SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        return client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        return request;
    }
}
