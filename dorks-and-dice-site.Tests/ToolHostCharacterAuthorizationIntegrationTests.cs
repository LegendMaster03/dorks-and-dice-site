using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostCharacterAuthorizationIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task CharacterSheetUpstreamTicketContainsOwnerCharacterAuthorizationProjection()
    {
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        Guid firstCampaignId;
        Guid secondCampaignId;
        Guid historicalCampaignId;
        Guid dmCampaignId;
        Guid activeCharacterId;
        Guid archivedCharacterId;
        Guid otherCharacterId;
        DateTimeOffset archivedAt;

        var capturingProxy = new CapturingToolProxyService();
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(capturingProxy);
            });
        });

        using (var scope = testFactory.Services.CreateScope())
        {
            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
            var characters = scope.ServiceProvider.GetRequiredService<ICharacterService>();

            var firstCampaign = await campaigns.CreateAsync(ownerUserId, $"Character Access A {Guid.NewGuid():N}");
            firstCampaignId = firstCampaign.Id;
            await campaigns.SetMemberRolesAsync(
                ownerUserId,
                firstCampaignId,
                ownerUserId,
                [CampaignRoles.Dm, CampaignRoles.Player]);

            var secondCampaign = await campaigns.CreateAsync(ownerUserId, $"Character Access B {Guid.NewGuid():N}");
            secondCampaignId = secondCampaign.Id;
            await campaigns.SetMemberRolesAsync(
                ownerUserId,
                secondCampaignId,
                ownerUserId,
                [CampaignRoles.Dm, CampaignRoles.Player]);

            var historicalCampaign = await campaigns.CreateAsync(ownerUserId, $"Character Access History {Guid.NewGuid():N}");
            historicalCampaignId = historicalCampaign.Id;
            await campaigns.SetMemberRolesAsync(
                ownerUserId,
                historicalCampaignId,
                ownerUserId,
                [CampaignRoles.Dm, CampaignRoles.Player]);

            var dmCampaign = await campaigns.CreateAsync(ownerUserId, $"Character Access DM {Guid.NewGuid():N}");
            dmCampaignId = dmCampaign.Id;
            await campaigns.AddMemberAsync(
                ownerUserId,
                dmCampaignId,
                otherUserId,
                [CampaignRoles.Player]);

            var activeCharacter = await characters.CreateAsync(ownerUserId, "Active Hero");
            activeCharacterId = activeCharacter.Id;
            await characters.ConnectToCampaignAsync(ownerUserId, activeCharacterId, firstCampaignId);
            await characters.ConnectToCampaignAsync(ownerUserId, activeCharacterId, secondCampaignId);
            await characters.ConnectToCampaignAsync(ownerUserId, activeCharacterId, historicalCampaignId);
            await characters.DisconnectFromCampaignAsync(ownerUserId, activeCharacterId, historicalCampaignId);

            var archivedCharacter = await characters.CreateAsync(ownerUserId, "Archived Hero");
            archivedCharacterId = archivedCharacter.Id;
            await characters.ConnectToCampaignAsync(ownerUserId, archivedCharacterId, firstCampaignId);
            await characters.ArchiveAsync(ownerUserId, archivedCharacterId);
            var archivedReloaded = await characters.GetAsync(ownerUserId, archivedCharacterId);
            Assert.NotNull(archivedReloaded);
            Assert.NotNull(archivedReloaded.ArchivedAt);
            archivedAt = archivedReloaded.ArchivedAt.Value;

            var otherCharacter = await characters.CreateAsync(otherUserId, "Another Player Hero");
            otherCharacterId = otherCharacter.Id;
            await characters.ConnectToCampaignAsync(otherUserId, otherCharacterId, dmCampaignId);
        }

        var registry = testFactory.Services.GetRequiredService<IToolRegistry>();
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = CharacterToolContract.Slug,
            DisplayName = CharacterToolContract.DisplayName,
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            UpstreamBaseUrl = "http://character-sheet:8080"
        };
        await registry.SaveAsync(tool);

        try
        {
            using var client = testFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://dorks-and-dice.com")
            });

            using var upstreamRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{CharacterToolContract.Slug}/api/upstream/characters/{otherCharacterId:D}/sheet?userId={otherUserId:D}");
            upstreamRequest.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            upstreamRequest.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, ownerUserId.ToString("D"));
            upstreamRequest.Headers.Add(ToolAuthenticationHeaders.Ticket, "browser-controlled-ticket");

            using var upstreamResponse = await client.SendAsync(upstreamRequest);
            Assert.Equal(HttpStatusCode.NoContent, upstreamResponse.StatusCode);
            Assert.NotNull(capturingProxy.AuthenticationTicket);
            Assert.NotEqual("browser-controlled-ticket", capturingProxy.AuthenticationTicket);
            Assert.Equal(
                $"/tool-host/{CharacterToolContract.Slug}/api/introspect",
                capturingProxy.IntrospectionPath);

            using var introspectionRequest = new HttpRequestMessage(
                HttpMethod.Post,
                capturingProxy.IntrospectionPath);
            introspectionRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                capturingProxy.AuthenticationTicket);
            using var introspectionResponse = await client.SendAsync(introspectionRequest);
            Assert.Equal(HttpStatusCode.OK, introspectionResponse.StatusCode);
            Assert.True(introspectionResponse.Headers.CacheControl?.NoStore);

            using var json = JsonDocument.Parse(await introspectionResponse.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal(1, root.GetProperty("contractVersion").GetInt32());
            Assert.Equal(CharacterToolContract.Slug, root.GetProperty("toolSlug").GetString());
            Assert.Equal(ownerUserId.ToString("D"), root.GetProperty("user").GetProperty("id").GetString());

            var characterEntries = root.GetProperty("characters").EnumerateArray().ToArray();
            Assert.Equal(2, characterEntries.Length);
            Assert.DoesNotContain(
                characterEntries,
                character => character.GetProperty("id").GetGuid() == otherCharacterId);

            var active = characterEntries.Single(character => character.GetProperty("id").GetGuid() == activeCharacterId);
            Assert.Equal("Active Hero", active.GetProperty("name").GetString());
            Assert.Equal(CharacterStatus.Active.ToString(), active.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, active.GetProperty("archivedAt").ValueKind);
            var activeCampaignIds = active.GetProperty("campaignIds")
                .EnumerateArray()
                .Select(value => value.GetGuid())
                .OrderBy(value => value)
                .ToArray();
            Assert.Equal(
                new[] { firstCampaignId, secondCampaignId }.OrderBy(value => value).ToArray(),
                activeCampaignIds);
            Assert.DoesNotContain(historicalCampaignId, activeCampaignIds);
            Assert.DoesNotContain(dmCampaignId, activeCampaignIds);

            var archived = characterEntries.Single(character => character.GetProperty("id").GetGuid() == archivedCharacterId);
            Assert.Equal("Archived Hero", archived.GetProperty("name").GetString());
            Assert.Equal(CharacterStatus.Archived.ToString(), archived.GetProperty("status").GetString());
            Assert.Equal(archivedAt, archived.GetProperty("archivedAt").GetDateTimeOffset());
            Assert.Empty(archived.GetProperty("campaignIds").EnumerateArray());

            var firstCampaignRoles = root.GetProperty("campaigns")
                .EnumerateArray()
                .Where(campaign => campaign.GetProperty("id").GetGuid() == firstCampaignId)
                .Select(campaign => campaign.GetProperty("role").GetString())
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "DM", "Player" }, firstCampaignRoles);
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task ExistingToolVersionOneIntrospectionOmitsCharacterProjection()
    {
        var context = new ToolHostAuthenticationContext
        {
            ToolSlug = "rules-core",
            SiteMode = SiteModeValues.DorksAndDiceModeValue,
            User = new ToolHostUserContext
            {
                Id = Guid.NewGuid().ToString("D"),
                DisplayName = "Compatibility User"
            },
            GlobalRoles = [],
            Campaigns =
            [
                new ToolHostCampaignAccessSummary
                {
                    Id = Guid.NewGuid(),
                    Name = "Compatibility Campaign",
                    Role = "DM"
                }
            ]
        };
        var ticket = ToolAuthenticationTickets.Issue(context);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/tool-host/rules-core/api/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("contractVersion").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("characters", out _));
        var campaign = Assert.Single(json.RootElement.GetProperty("campaigns").EnumerateArray());
        Assert.Equal("DM", campaign.GetProperty("role").GetString());
    }

    private sealed class CapturingToolProxyService : IToolProxyService
    {
        public string? AuthenticationTicket { get; private set; }
        public string? IntrospectionPath { get; private set; }

        public Task ProxyAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }

        public Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            AuthenticationTicket = authenticationTicket;
            IntrospectionPath = introspectionPath;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }
}
