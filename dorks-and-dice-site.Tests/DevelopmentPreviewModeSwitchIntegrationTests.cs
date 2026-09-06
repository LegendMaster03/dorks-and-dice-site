using System.Net;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task DevelopmentPreviewHomepageFollowsSelectedNormalMode()
    {
        using var factory = new PublishedContentWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();

            var professional = authoring.GetNew("External");
            professional.Document.Id = "professional-home-preview-test";
            professional.Document.Slug = "professional-home-preview-test";
            professional.Document.TagsText = ContentTags.Homepage;
            professional.Document.VisibleModesSelection = [BuiltInSiteModes.Professional.Id];
            professional.Document.Body = "# PROFESSIONAL HOMEPAGE MARKER";
            await authoring.CreateAsync(professional.Document);

            var dorks = authoring.GetNew("External");
            dorks.Document.Id = "dorks-home-preview-test";
            dorks.Document.Slug = "dorks-home-preview-test";
            dorks.Document.TagsText = ContentTags.Homepage;
            dorks.Document.VisibleModesSelection = [BuiltInSiteModes.DorksAndDice.Id];
            dorks.Document.Body = "# DORKS HOMEPAGE MARKER";
            await authoring.CreateAsync(dorks.Document);
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var dorksRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        dorksRequest.Headers.Host = "localhost";
        dorksRequest.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        dorksRequest.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=dorks-and-dice");
        var dorksResponse = await client.SendAsync(dorksRequest);
        var dorksHtml = await dorksResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dorksResponse.StatusCode);
        Assert.Contains("DORKS HOMEPAGE MARKER", dorksHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("PROFESSIONAL HOMEPAGE MARKER", dorksHtml, StringComparison.Ordinal);

        using var professionalRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        professionalRequest.Headers.Host = "localhost";
        professionalRequest.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Dev);
        professionalRequest.Headers.Add("Cookie", "DevelopmentPreviewSiteMode=professional");
        var professionalResponse = await client.SendAsync(professionalRequest);
        var professionalHtml = await professionalResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, professionalResponse.StatusCode);
        Assert.Contains("PROFESSIONAL HOMEPAGE MARKER", professionalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("DORKS HOMEPAGE MARKER", professionalHtml, StringComparison.Ordinal);
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
