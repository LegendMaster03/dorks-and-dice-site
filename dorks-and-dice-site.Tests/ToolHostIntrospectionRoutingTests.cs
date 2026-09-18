using System.Net;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostIntrospectionRoutingTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ToolHostIntrospectionRoutingTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FrameworkFallbackAllowsOnlyToolHostIntrospectionFromToolHostApiSurface()
    {
        Assert.True(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/rules-core/api/introspect")));
        Assert.True(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/character-sheet/api/delegate/rules-core/upstream")));
        Assert.True(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/character-sheet/api/delegate/rules-core/upstream/api/rules")));

        Assert.False(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/rules-core/api/session")));
        Assert.False(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/rules-core/context")));
        Assert.False(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/rules-core/extra/api/introspect")));
        Assert.False(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/character-sheet/api/delegate/rules-core")));
        Assert.False(SiteRouteOwnership.IsAllowedInFrameworkFallback(
            new PathString("/tool-host/character-sheet/api/session")));
    }

    [Fact]
    public async Task DelegatedUpstreamEndpointIsReachableThroughInternalDockerHostWithoutSiteMode()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/tool-host/character-sheet/api/delegate/rules-core/upstream/api/rules");
        request.Headers.Host = "dorks-and-dice-site";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task IntrospectionEndpointIsReachableThroughInternalDockerHostWithoutSiteMode()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/tool-host/rules-core/api/introspect");
        request.Headers.Host = "dorks-and-dice-site";

        using var response = await client.SendAsync(request);

        // No bearer ticket was supplied, so reaching the endpoint must produce its explicit
        // authentication failure rather than the SiteMode framework fallback 404 page.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
