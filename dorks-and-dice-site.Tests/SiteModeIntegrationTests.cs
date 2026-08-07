using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
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
    [InlineData("kylebarnett.com", "/site-modes/professional/css/site.css", HttpStatusCode.OK)]
    [InlineData("kylebarnett.com", "/site-modes/professional/images/favicon.svg", HttpStatusCode.OK)]
    [InlineData("kylebarnett.com", "/site-modes/dorks-and-dice/images/sample.png", HttpStatusCode.NotFound)]
    [InlineData("kylebarnett.com", "/site-modes/dorks-and-dice/images/favicon.svg", HttpStatusCode.NotFound)]
    [InlineData("dorks-and-dice.com", "/", HttpStatusCode.OK)]
    [InlineData("dorks-and-dice.com", "/resume", HttpStatusCode.NotFound)]
    [InlineData("dorks-and-dice.com", "/site-modes/dorks-and-dice/css/site.css", HttpStatusCode.OK)]
    [InlineData("dorks-and-dice.com", "/site-modes/dorks-and-dice/images/favicon.svg", HttpStatusCode.OK)]
    [InlineData("dorks-and-dice.com", "/site-modes/professional/files/kyle-resume.pdf", HttpStatusCode.NotFound)]
    [InlineData("dorks-and-dice.com", "/site-modes/professional/images/favicon.svg", HttpStatusCode.NotFound)]
    [InlineData("unassigned.example", "/", HttpStatusCode.OK)]
    [InlineData("unassigned.example", "/favicon.ico", HttpStatusCode.OK)]
    [InlineData("unassigned.example", "/resume", HttpStatusCode.NotFound)]
    [InlineData("unassigned.example", "/site-modes/professional/images/profile/kyle-headshot.jpg", HttpStatusCode.NotFound)]
    public async Task HostModeRouteMatrixReturnsExpectedStatus(string host, string path, HttpStatusCode expectedStatusCode)
    {
        var response = await SendAsync(host, path);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Theory]
    [InlineData(SiteMode.Professional)]
    [InlineData(SiteMode.DorksAndDice)]
    [InlineData(SiteMode.Development)]
    [InlineData(SiteMode.Unassigned)]
    public void UnassignedAssetsAreSharedFallbackAssets(SiteMode siteMode)
    {
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/site-modes/unassigned/images/fallback.svg", siteMode));
    }

    [Fact]
    public async Task GeneratedScopedStylesheetIsAllowedAsSharedStaticAsset()
    {
        var response = await SendAsync("kylebarnett.com", "/dorks-and-dice-site.styles.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("kylebarnett.com", "/site-modes/professional/css/site.css", "/site-modes/dorks-and-dice/css/site.css")]
    [InlineData("dorks-and-dice.com", "/site-modes/dorks-and-dice/css/site.css", "/site-modes/professional/css/site.css")]
    public async Task RealDomainsLoadOnlyTheirModeStylesheet(string host, string expectedStylesheet, string excludedStylesheet)
    {
        var response = await SendAsync(host, "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/css/site.css", html);
        Assert.Contains(expectedStylesheet, html);
        Assert.DoesNotContain(excludedStylesheet, html);
        Assert.DoesNotContain("/site-modes/development/css/site.css", html);
    }

    [Theory]
    [InlineData("kylebarnett.com", "/site-modes/professional/images/favicon.svg", "/site-modes/dorks-and-dice/images/favicon.svg")]
    [InlineData("dorks-and-dice.com", "/site-modes/dorks-and-dice/images/favicon.svg", "/site-modes/professional/images/favicon.svg")]
    [InlineData("unassigned.example", "/favicon.ico", "/site-modes/professional/images/favicon.svg")]
    public async Task RealDomainsLoadTheirModeFavicon(string host, string expectedFavicon, string excludedFavicon)
    {
        var response = await SendAsync(host, "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"""<link rel="icon" href="{expectedFavicon}""", html);
        Assert.DoesNotContain(excludedFavicon, html);
    }

    [Fact]
    public async Task UnassignedModeLoadsOnlySharedStylesheet()
    {
        var response = await SendAsync("unassigned.example", "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/css/site.css", html);
        Assert.DoesNotContain("/site-modes/professional/css/site.css", html);
        Assert.DoesNotContain("/site-modes/dorks-and-dice/css/site.css", html);
        Assert.DoesNotContain("/site-modes/development/css/site.css", html);
    }

    [Fact]
    public async Task DevelopmentPreviewLoadsSelectedModeAndDevelopmentToolsStylesheets()
    {
        using var request = CreateRequest("localhost", "/");
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=dorks-and-dice");

        var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/site-modes/dorks-and-dice/css/site.css", html);
        Assert.Contains("/site-modes/dorks-and-dice/images/favicon.svg", html);
        Assert.Contains("/site-modes/development/css/site.css", html);
        Assert.DoesNotContain("/site-modes/professional/css/site.css", html);
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
    public async Task FilterableIndexesExposeLiveStatusRegions()
    {
        var resumeResponse = await SendAsync("kylebarnett.com", "/resume");
        var resumeHtml = await resumeResponse.Content.ReadAsStringAsync();
        var articlesResponse = await SendAsync("kylebarnett.com", "/articles");
        var articlesHtml = await articlesResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        Assert.Contains("id=\"projectFilterStatus\"", resumeHtml);
        Assert.Equal(HttpStatusCode.OK, articlesResponse.StatusCode);
        Assert.Contains("id=\"articleFilterStatus\"", articlesHtml);
    }

    [Fact]
    public async Task ProfessionalProjectsExposeUserConfigurableTagFilters()
    {
        var response = await SendAsync("kylebarnett.com", "/resume");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-filter-tag=\"all\"", html);
        Assert.Contains("data-filter-tag=\"architecture\"", html);
        Assert.Contains("data-filter-tag=\"web-development\"", html);
        Assert.Contains("list=\"projectTagSuggestions\"", html);
        Assert.Contains("<option value=\"architecture\">", html);
        Assert.Contains("id=\"projectList\"", html);
        Assert.Contains("data-featured=\"true\"", html);
        Assert.Contains("data-search=\"", html);
        Assert.Contains("data-title=\"personal multi-mode website\"", html);
        Assert.DoesNotContain("data-filter=\"professional\"", html);
    }

    [Fact]
    public async Task ArticleIndexExposesUserConfigurableTagFiltersInDevelopmentPreview()
    {
        using var request = CreateRequest("localhost", "/articles");
        request.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=development; DevelopmentIncludeUnlistedArticles=true");

        var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-article-tag=\"all\"", html);
        Assert.Contains("data-article-tag=\"technical-investigation\"", html);
        Assert.Contains("data-article-tags=\"technical-investigation puzzle write-up\"", html);
        Assert.Contains("list=\"articleTagSuggestions\"", html);
        Assert.Contains("<option value=\"technical-investigation\">", html);
        Assert.Contains("id=\"articleList\"", html);
        Assert.Contains("data-article-listed=\"false\"", html);
        Assert.Contains("data-article-date=\"august 2026\"", html);
        Assert.Contains("data-article-title=\"freeing the bees: solving consolevariations&#x27; hidden web puzzle\"", html);
    }

    [Fact]
    public async Task ProfessionalProjectsRenderAsOneFilterableList()
    {
        var response = await SendAsync("kylebarnett.com", "/resume");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Featured projects are repeated here intentionally", html);
        Assert.DoesNotContain("Also shown above in Featured Projects", html);
    }

    [Fact]
    public async Task SkyblivionPageUsesSharedImageModal()
    {
        var response = await SendAsync("kylebarnett.com", "/resume/skyblivion");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"imageModal\"", html);
    }

    [Fact]
    public async Task PersonalMultiModeWebsiteShowsLiveArchitectureMatrix()
    {
        var response = await SendAsync("kylebarnett.com", "/resume/personalmultimodewebsite");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Architecture Flow", html);
        Assert.Contains("Resolve Mode", html);
        Assert.Contains("Check Access", html);
        Assert.Contains("Fallback Piece", html);
        Assert.Contains("Live Mode Matrix", html);
        Assert.Contains("Representative Access Rules", html);
        Assert.Contains("Resume, portfolio, and professional article identity", html);
        Assert.Contains("Community-facing identity", html);
        Assert.Contains("<th scope=\"row\">Professional</th>", html);
        Assert.Contains("<th scope=\"row\">Community</th>", html);
        Assert.Contains("<th scope=\"row\">Development</th>", html);
        Assert.Contains("<th scope=\"row\">Unassigned</th>", html);
        Assert.Contains("professional resume surface", html);
        Assert.Contains("professional-owned asset", html);
        Assert.Contains("Allowed", html);
        Assert.Contains("Blocked", html);
        Assert.DoesNotContain("~/Views/SiteModes/Professional/Branding/_Header.cshtml", html);
        Assert.DoesNotContain("~/site-modes/professional/css/site.css", html);
        Assert.DoesNotContain("<code>/resume</code>", html);
        Assert.DoesNotContain("10.0.0.7", html);
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
