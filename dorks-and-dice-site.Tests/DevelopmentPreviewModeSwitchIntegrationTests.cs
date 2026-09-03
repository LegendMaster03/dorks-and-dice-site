using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class DevelopmentPreviewModeSwitchIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public DevelopmentPreviewModeSwitchIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(SiteMode.Development)]
    [InlineData(SiteMode.Professional)]
    [InlineData(SiteMode.DorksAndDice)]
    [InlineData(SiteMode.Unassigned)]
    public void DevelopmentPreviewEndpointIsSharedAcrossModes(SiteMode mode)
    {
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/development-preview", mode));
    }

    [Theory]
    [InlineData(SiteMode.Development)]
    [InlineData(SiteMode.Professional)]
    [InlineData(SiteMode.DorksAndDice)]
    [InlineData(SiteMode.Unassigned)]
    public void DevelopmentWorkspaceRoutesAreSharedAcrossModes(SiteMode mode)
    {
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/development", mode));
        Assert.True(SiteRouteOwnership.IsAllowedInMode("/development/databases", mode));
    }

    [Theory]
    [InlineData("development", "professional")]
    [InlineData("professional", "dorks-and-dice")]
    [InlineData("dorks-and-dice", "development")]
    public async Task TrustedAnonymousUserCanSwitchModes(string currentMode, string requestedMode)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/development-preview");
        request.Headers.Host = "localhost";
        request.Headers.Add("Cookie", $"DevelopmentPreviewSiteMode={currentMode}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["siteMode"] = requestedMode,
            ["returnUrl"] = "/"
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith($"DevelopmentPreviewSiteMode={requestedMode}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UntrustedAnonymousUserCanNotUseDevelopmentPreviewEndpoint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "http://dorks-and-dice.com/development-preview");
        request.Headers.Host = "dorks-and-dice.com";
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["siteMode"] = "professional",
            ["returnUrl"] = "/"
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(value => value.StartsWith("DevelopmentPreviewSiteMode=", StringComparison.Ordinal)));
    }
}
