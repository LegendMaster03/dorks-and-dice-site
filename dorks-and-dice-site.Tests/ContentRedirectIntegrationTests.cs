using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ContentRedirectIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ContentRedirectIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LegacyRouteRedirectsPermanentlyToTheCurrentCanonicalSlug()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "http://kylebarnett.com/resume/ExperienceSkyblivion?context=project&ref=legacy");
        request.Headers.Host = "kylebarnett.com";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(
            "/resume/skyblivion?context=project&ref=legacy",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RedirectAliasesDoNotCrossRouteNamespaces()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "http://kylebarnett.com/articles/experienceskyblivion");
        request.Headers.Host = "kylebarnett.com";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RedirectDoesNotBypassTargetVisibilityForTheActiveMode()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "http://dorks-and-dice.com/articles/legacy-bees-article");
        request.Headers.Host = "dorks-and-dice.com";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
