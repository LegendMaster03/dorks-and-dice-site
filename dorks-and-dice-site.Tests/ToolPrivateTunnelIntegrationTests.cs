using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolPrivateTunnelIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task ConfiguredPrivateTunnelIssuesDirectTargetTicketWithoutGrantingDelegation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var source = Tool($"private-source-{suffix}");
        var target = Tool($"private-target-{suffix}");

        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{ToolPrivateTunnelPolicy.ConfigurationSection}:{source.Key}:0"] = target.Key
                });
            });
        });

        await SaveToolsAsync(testFactory.Services, source, target);
        try
        {
            var sourceContext = new ToolHostAuthenticationContext
            {
                ToolKey = source.Key,
                ToolSlug = source.Slug,
                SiteMode = "dorks-and-dice",
                User = new ToolHostUserContext
                {
                    Id = Guid.NewGuid().ToString("D"),
                    DisplayName = "Private Tunnel User"
                }
            };
            var sourceTicket = ToolAuthenticationTickets.Issue(sourceContext);
            using var client = testFactory.CreateClient();

            using var sourceIntrospection = await IntrospectAsync(
                client,
                source.Slug!,
                sourceTicket);
            Assert.Equal(HttpStatusCode.OK, sourceIntrospection.StatusCode);
            Assert.False(sourceIntrospection.Headers.Contains(ToolDelegationHeaders.Capability));
            Assert.True(sourceIntrospection.Headers.TryGetValues(
                ToolPrivateTunnelHeaders.Capability,
                out var privateCapabilities));
            var capability = Assert.Single(privateCapabilities);

            using var issueRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/{source.Slug}/api/private-tunnel/{target.Key}/ticket");
            issueRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", capability);
            using var issueResponse = await client.SendAsync(issueRequest);
            Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);
            Assert.Equal("no-store", issueResponse.Headers.CacheControl?.ToString());

            var ticket = JsonSerializer.Deserialize<ToolPrivateTunnelTicketResponse>(
                await issueResponse.Content.ReadAsStringAsync(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(ticket);
            Assert.Equal(target.Key, ticket.TargetKey);
            Assert.Equal($"/tool-host/{target.Slug}/api/introspect", ticket.IntrospectionPath);
            Assert.False(string.IsNullOrWhiteSpace(ticket.Ticket));

            using var targetIntrospection = await IntrospectAsync(
                client,
                target.Slug!,
                ticket.Ticket);
            Assert.Equal(HttpStatusCode.OK, targetIntrospection.StatusCode);

            using var targetJson = JsonDocument.Parse(
                await targetIntrospection.Content.ReadAsStringAsync());
            var root = targetJson.RootElement;
            Assert.Equal(target.Key, root.GetProperty("toolKey").GetString());
            Assert.Equal(source.Key, root.GetProperty("privateTunnelSourceToolKey").GetString());
            Assert.Equal(source.Slug, root.GetProperty("privateTunnelSourceToolSlug").GetString());
            Assert.False(root.TryGetProperty("delegatedFromToolKey", out _));
            Assert.False(root.TryGetProperty("delegatedFromToolSlug", out _));

            using var delegatedRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/{target.Key}/upstream/api/private");
            delegatedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", capability);
            using var delegatedResponse = await client.SendAsync(delegatedRequest);
            Assert.Equal(HttpStatusCode.Forbidden, delegatedResponse.StatusCode);
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, target);
        }
    }

    [Fact]
    public async Task PrivateTunnelRejectsUnconfiguredTarget()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var source = Tool($"private-source-{suffix}");
        var approvedTarget = Tool($"approved-target-{suffix}");
        var deniedTarget = Tool($"denied-target-{suffix}");

        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{ToolPrivateTunnelPolicy.ConfigurationSection}:{source.Key}:0"] = approvedTarget.Key
                });
            });
        });

        await SaveToolsAsync(testFactory.Services, source, approvedTarget, deniedTarget);
        try
        {
            var sourceContext = new ToolHostAuthenticationContext
            {
                ToolKey = source.Key,
                ToolSlug = source.Slug,
                SiteMode = "dorks-and-dice",
                User = new ToolHostUserContext
                {
                    Id = Guid.NewGuid().ToString("D"),
                    DisplayName = "Private Tunnel User"
                }
            };
            var capability = testFactory.Services
                .GetRequiredService<IToolDelegationCapabilityService>()
                .Issue(source.Key, sourceContext);
            using var client = testFactory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/{source.Slug}/api/private-tunnel/{deniedTarget.Key}/ticket");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", capability);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, approvedTarget, deniedTarget);
        }
    }

    private static ToolRegistration Tool(string key) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Kind = ToolKind.Application,
        Slug = key,
        DisplayName = key,
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = $"http://{key}:8080",
        FrontendEntryPoint = "/app.js",
        HealthPath = "/ready",
        Modes = ["dorks-and-dice"],
        AllowAnonymous = false,
        Enabled = true
    };

    private static async Task SaveToolsAsync(
        IServiceProvider services,
        params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            await registry.SaveAsync(tool);
        }
    }

    private static async Task DeleteToolsAsync(
        IServiceProvider services,
        params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    private static async Task<HttpResponseMessage> IntrospectAsync(
        HttpClient client,
        string slug,
        string ticket)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/tool-host/{slug}/api/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        return await client.SendAsync(request);
    }
}
