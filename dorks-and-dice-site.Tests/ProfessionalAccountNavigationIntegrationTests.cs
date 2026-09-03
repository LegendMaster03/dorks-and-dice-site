using System.Net;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ProfessionalAccountNavigationIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ProfessionalAccountNavigationIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousProfessionalHeaderDoesNotShowLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://kylebarnett.com/");
        request.Headers.Host = "kylebarnett.com";

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(">Log in<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Account settings", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedInProfessionalHeaderShowsNormalAccountMenu()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://kylebarnett.com/");
        request.Headers.Host = "kylebarnett.com";
        request.Headers.Add(
            TestRoleAuthenticationHandler.ScopedRolesHeader,
            $"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Account settings", html, StringComparison.Ordinal);
        Assert.Contains(">Editor<", html, StringComparison.Ordinal);
        Assert.Contains(">Log out<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Log in<", html, StringComparison.Ordinal);
    }
}
