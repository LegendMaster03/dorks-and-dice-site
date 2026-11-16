using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// Source-scoped backend capabilities do not authorize a stale character
/// snapshot. Every lookup must query Site-owned character state again.
/// </summary>
[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class CharacterToolFreshAccessTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task FreshBackendLookupObservesArchiveAndCampaignDisconnect()
    {
        var actor = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var source = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = CharacterToolContract.Slug,
            Slug = CharacterToolContract.Slug,
            DisplayName = "Character Sheet test",
            Kind = ToolKind.Application,
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://character-sheet:8080",
            FrontendEntryPoint = "/app.js",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            DelegationTargets = ["rules-core"],
            AllowAnonymous = false,
            Enabled = true
        };
        Guid characterId;
        Guid campaignId;
        using (var scope = factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            await registry.SaveAsync(source);
            var characters = scope.ServiceProvider.GetRequiredService<ICharacterService>();
            characterId = (await characters.CreateAsync(actor,
                "Fresh Site access test " + Guid.NewGuid().ToString("N"))).Id;
            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
            var campaign = await campaigns.CreateAsync(
                actor, "Fresh scope " + Guid.NewGuid().ToString("N"));
            campaignId = campaign.Id;
            await campaigns.SetMemberRolesAsync(actor, campaignId, actor, [CampaignRoles.Player]);
            await characters.ConnectToCampaignAsync(actor, characterId, campaignId);
        }

        try
        {
            var context = new ToolHostAuthenticationContext
            {
                ToolKey = CharacterToolContract.Slug,
                ToolSlug = CharacterToolContract.Slug,
                SiteMode = SiteModeValues.DorksAndDiceModeValue,
                User = new ToolHostUserContext
                {
                    Id = actor.ToString("D"), DisplayName = "Live owner"
                }
            };
            var capability = factory.Services.GetRequiredService<IToolDelegationCapabilityService>()
                .Issue(CharacterToolContract.Slug, context);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://dorks-and-dice.com")
            });
            var path = $"/tool-host/registrations/character-sheet/api/characters/{characterId:D}/fresh-access";

            async Task<(HttpStatusCode Status, System.Text.Json.JsonElement? Body)> Read(
                string? token)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path);
                if (token is not null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                var body = response.IsSuccessStatusCode
                    ? await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()
                    : (System.Text.Json.JsonElement?)null;
                Assert.True(response.Headers.CacheControl?.NoStore);
                return (response.StatusCode, body);
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await Read(null)).Status);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Read("not-a-site-capability")).Status);

            var active = await Read(capability);
            Assert.Equal(HttpStatusCode.OK, active.Status);
            Assert.Equal(actor.ToString("D"),
                active.Body!.Value.GetProperty("actorUserId").GetString());
            Assert.Equal(characterId.ToString("D"),
                active.Body.Value.GetProperty("characterId").GetString());
            Assert.Equal("Active", active.Body.Value.GetProperty("status").GetString());
            Assert.True(active.Body.Value.GetProperty("allowsOrdinaryEditing").GetBoolean());
            Assert.Equal(campaignId.ToString("D"),
                Assert.Single(active.Body.Value.GetProperty("campaignIds")
                    .EnumerateArray()).GetString());

            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ICharacterService>()
                    .DisconnectFromCampaignAsync(actor, characterId, campaignId);

            var disconnected = await Read(capability);
            Assert.Equal(HttpStatusCode.OK, disconnected.Status);
            Assert.Empty(disconnected.Body!.Value.GetProperty("campaignIds").EnumerateArray());

            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ICharacterService>()
                    .ArchiveAsync(actor, characterId);

            var archived = await Read(capability);
            Assert.Equal(HttpStatusCode.OK, archived.Status);
            Assert.Equal("Archived", archived.Body!.Value.GetProperty("status").GetString());
            Assert.False(archived.Body.Value.GetProperty("allowsOrdinaryEditing").GetBoolean());
            Assert.Equal(System.Text.Json.JsonValueKind.String,
                archived.Body.Value.GetProperty("archivedAt").ValueKind);

            var otherContext = context with
            {
                User = new ToolHostUserContext
                {
                    Id = secondUser.ToString("D"), DisplayName = "Other owner"
                }
            };
            var otherCapability = factory.Services
                .GetRequiredService<IToolDelegationCapabilityService>()
                .Issue(CharacterToolContract.Slug, otherContext);
            Assert.Equal(HttpStatusCode.NotFound, (await Read(otherCapability)).Status);

            source.Enabled = false;
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IToolRegistry>()
                    .SaveAsync(source);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Read(capability)).Status);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IToolRegistry>()
                .DeleteAsync(source.Id);
        }
    }
}
