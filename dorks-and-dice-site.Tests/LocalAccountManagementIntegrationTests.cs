using System.Net;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class LocalAccountManagementIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public LocalAccountManagementIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TrustedLocalAdminCanOpenAccountManagementAgainstSqliteIdentityStore()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/admin/accounts");
        request.Headers.Host = "localhost";
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Admin);

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Account administration", html, StringComparison.Ordinal);
    }
}
