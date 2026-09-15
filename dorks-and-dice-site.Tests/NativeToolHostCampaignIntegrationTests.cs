using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class NativeToolHostCampaignIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task NativeCampaignContextFeedsToolHostRoster()
    {
        var userId = Guid.NewGuid();
        Guid campaignId;
        Guid participantId;
        Guid characterId;

        using (var scope = factory.Services.CreateScope())
        {
            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
            var participants = scope.ServiceProvider.GetRequiredService<ICampaignParticipantService>();
            var characters = scope.ServiceProvider.GetRequiredService<ICharacterService>();

            var campaign = await campaigns.CreateAsync(userId, $"Tool Host {Guid.NewGuid():N}");
            campaignId = campaign.Id;
            await campaigns.SetMemberRolesAsync(
                userId,
                campaign.Id,
                userId,
                [CampaignRoles.Dm, CampaignRoles.Player]);

            var participant = await participants.AddGuestAsync(userId, campaign.Id, "Guest Player");
            participantId = participant.Id;

            var character = await characters.CreateAsync(userId, "Campaign Hero");
            characterId = character.Id;
            await characters.ConnectToCampaignAsync(userId, character.Id, campaign.Id);
        }

        var registry = factory.Services.GetRequiredService<IToolRegistry>();
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"native-campaign-{Guid.NewGuid():N}",
            DisplayName = "Native Campaign Tool",
            Enabled = true,
            AllowAnonymous = true,
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = "http://native-campaign-tool:8080"
        };
        await registry.SaveAsync(tool);

        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString());

            using var list = await client.GetAsync($"/tool-host/{tool.Slug}/api/campaigns");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.True(list.Headers.CacheControl?.NoStore);
            using (var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            {
                var campaign = Assert.Single(listJson.RootElement.EnumerateArray());
                Assert.Equal(campaignId, campaign.GetProperty("id").GetGuid());
                Assert.Equal("DM", campaign.GetProperty("role").GetString());
            }

            using var context = await client.GetAsync(
                $"/tool-host/{tool.Slug}/api/campaigns/{campaignId}/context");
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            Assert.True(context.Headers.CacheControl?.NoStore);
            using var contextJson = JsonDocument.Parse(await context.Content.ReadAsStringAsync());
            var root = contextJson.RootElement;
            Assert.Equal(campaignId, root.GetProperty("campaignId").GetGuid());

            var roles = root.GetProperty("requestingUserRoles")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();
            Assert.Contains(CampaignRoles.Dm, roles);
            Assert.Contains(CampaignRoles.Player, roles);

            var participant = Assert.Single(root.GetProperty("participants").EnumerateArray());
            Assert.Equal(participantId, participant.GetProperty("participantId").GetGuid());
            Assert.Equal("Guest Player", participant.GetProperty("displayName").GetString());

            var character = Assert.Single(root.GetProperty("characters").EnumerateArray());
            Assert.Equal(characterId, character.GetProperty("characterId").GetGuid());
            Assert.Equal("Campaign Hero", character.GetProperty("name").GetString());
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
            using var scope = factory.Services.CreateScope();
            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
            await campaigns.ArchiveAsync(userId, campaignId);
        }
    }
}
