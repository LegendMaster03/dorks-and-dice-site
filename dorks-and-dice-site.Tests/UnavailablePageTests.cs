using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class UnavailablePageTests(PublishedContentWebApplicationFactory factory)
{
    [Theory]
    [InlineData("/a-page-that-does-not-exist?tab=summary", "%2Fa-page-that-does-not-exist%3Ftab%3Dsummary")]
    [InlineData("/resume?tab=summary", "%2Fresume%3Ftab%3Dsummary")]
    public async Task AnonymousPagesOfferSignInWithoutDisclosingExistence(string path, string encodedTarget)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("This page may not exist, or you may need to sign in", html);
        Assert.Contains("/account/login", html);
        Assert.Contains(encodedTarget, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The page you requested could not be found.", html);
    }

    [Fact]
    public async Task SignedInMissingPageDoesNotSuggestSigningInAgain()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/a-page-that-does-not-exist");
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The page you requested could not be found.", html);
        Assert.DoesNotContain("Sign in</a>", html);
    }
}
