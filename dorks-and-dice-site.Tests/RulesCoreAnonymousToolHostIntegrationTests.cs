using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class RulesCoreAnonymousToolHostIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public RulesCoreAnonymousToolHostIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RulesCoreContextRemainsPublicWhenLegacyRegistrationStoredFalse()
    {
        var registration = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = "rules-core",
            DisplayName = "Rules Core",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = "http://rules-core:8080",
            FrontendEntryPoint = "/app.js",
            HealthPath = "/health",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            var existing = await registry.GetBySlugAsync(registration.Slug);
            if (existing is not null)
            {
                await registry.DeleteAsync(existing.Id);
            }
            await registry.SaveAsync(registration);
        }

        try
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/tool-host/rules-core/context");
            request.Headers.Host = "dorks-and-dice.com";
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("rules-core", body.RootElement.GetProperty("toolSlug").GetString());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("user").ValueKind);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            var stored = await registry.GetBySlugAsync(registration.Slug);
            if (stored is not null)
            {
                await registry.DeleteAsync(stored.Id);
            }
        }
    }
}
