using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostRulesAuthorizationIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ToolHostRulesAuthorizationIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SessionExposesEffectiveRulesLawyerAuthorityToAuthenticatedToolUi()
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"rules-auth-{Guid.NewGuid():N}",
            DisplayName = "Rules authorization test",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true
        };

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{tool.Slug}/api/session");
            request.Headers.Host = "dorks-and-dice.com";
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Owner);

            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            using var response = await client.SendAsync(request);
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var roles = json.RootElement.GetProperty("globalRoles")
                .EnumerateArray()
                .Select(value => value.GetString())
                .Where(value => value is not null)
                .ToArray();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(AccountRoles.RulesLawyer, roles);
            Assert.Equal("integration-test-user", json.RootElement.GetProperty("user").GetProperty("id").GetString());
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(tool.Id);
        }
    }
}
