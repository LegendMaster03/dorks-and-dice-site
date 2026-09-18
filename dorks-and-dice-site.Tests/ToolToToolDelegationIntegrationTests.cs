using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolToToolDelegationIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task ApprovedDelegationPreservesUserAuthorityAndRebuildsTargetContext()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var capturingProxy = new CapturingToolProxyService();
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(capturingProxy);
            });
        });

        var source = Tool(
            CharacterToolContract.Slug,
            "http://character-sheet:8080",
            [SiteModeValues.DorksAndDiceModeValue],
            delegationTargets: ["rules-core"]);
        var target = Tool(
            "rules-core",
            "http://rules-core:8080",
            [SiteModeValues.DorksAndDiceModeValue]);

        using (var scope = testFactory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            await registry.SaveAsync(source);
            await registry.SaveAsync(target);

            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
            var campaign = await campaigns.CreateAsync(
                userId,
                $"Delegation Campaign {Guid.NewGuid():N}");
            await campaigns.SetMemberRolesAsync(
                userId,
                campaign.Id,
                userId,
                [CampaignRoles.Dm, CampaignRoles.Player]);
        }

        try
        {
            using var client = Client(testFactory);

            using var sourceRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/upstream/characters");
            Authenticate(sourceRequest, userId, AccountRoles.RulesLawyer);
            using var sourceResponse = await client.SendAsync(sourceRequest);
            Assert.Equal(HttpStatusCode.NoContent, sourceResponse.StatusCode);

            var sourceCall = Assert.Single(capturingProxy.Calls);
            Assert.Equal(source.Slug, sourceCall.ToolSlug);
            Assert.Equal($"/tool-host/{source.Slug}/api/introspect", sourceCall.IntrospectionPath);

            using var sourceIntrospection = await IntrospectAsync(
                client,
                source.Slug,
                sourceCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, sourceIntrospection.StatusCode);
            Assert.True(sourceIntrospection.Headers.CacheControl?.NoStore);
            Assert.True(sourceIntrospection.Headers.TryGetValues(
                ToolDelegationHeaders.Capability,
                out var capabilityValues));
            var capability = Assert.Single(capabilityValues);
            Assert.True(sourceIntrospection.Headers.TryGetValues(
                ToolDelegationHeaders.Path,
                out var pathValues));
            Assert.Equal(
                $"/tool-host/{source.Slug}/api/delegate/{{targetSlug}}/upstream",
                Assert.Single(pathValues));

            using var sourceJson = JsonDocument.Parse(
                await sourceIntrospection.Content.ReadAsStringAsync());
            Assert.Equal(source.Slug, sourceJson.RootElement.GetProperty("toolSlug").GetString());
            Assert.True(sourceJson.RootElement.TryGetProperty("characters", out _));

            using var sourceReplay = await IntrospectAsync(
                client,
                source.Slug,
                sourceCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.Unauthorized, sourceReplay.StatusCode);

            const string requestBody = "delegated-request-body";
            using var delegatedRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/{source.Slug}/api/delegate/{target.Slug}/upstream/api/rules?userId={otherUserId:D}&include=all")
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "text/plain")
            };
            delegatedRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", capability);
            delegatedRequest.Headers.TryAddWithoutValidation(
                "Cookie",
                "__Host-dorks-and-dice.auth=browser-cookie-must-not-establish-identity");
            delegatedRequest.Headers.TryAddWithoutValidation(
                ToolDelegationHeaders.Path,
                "/browser-spoof");

            using var delegatedResponse = await client.SendAsync(delegatedRequest);
            Assert.Equal(HttpStatusCode.NoContent, delegatedResponse.StatusCode);
            Assert.Equal("no-store", delegatedResponse.Headers.CacheControl?.ToString());

            Assert.Equal(2, capturingProxy.Calls.Count);
            var targetCall = capturingProxy.Calls[1];
            Assert.Equal(target.Slug, targetCall.ToolSlug);
            Assert.Equal("/api/rules", targetCall.Path);
            Assert.Equal(
                $"?userId={otherUserId:D}&include=all",
                targetCall.QueryString);
            Assert.Equal(requestBody, targetCall.RequestBody);
            Assert.Equal(
                $"/tool-host/{target.Slug}/api/introspect",
                targetCall.IntrospectionPath);
            Assert.NotEqual(sourceCall.AuthenticationTicket, targetCall.AuthenticationTicket);

            using var targetIntrospection = await IntrospectAsync(
                client,
                target.Slug,
                targetCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, targetIntrospection.StatusCode);
            Assert.False(targetIntrospection.Headers.Contains(
                ToolDelegationHeaders.Capability));

            using var targetJson = JsonDocument.Parse(
                await targetIntrospection.Content.ReadAsStringAsync());
            var root = targetJson.RootElement;
            Assert.Equal(target.Slug, root.GetProperty("toolSlug").GetString());
            Assert.Equal(
                SiteModeValues.DorksAndDiceModeValue,
                root.GetProperty("siteMode").GetString());
            Assert.Equal(
                userId.ToString("D"),
                root.GetProperty("user").GetProperty("id").GetString());
            Assert.Contains(
                AccountRoles.RulesLawyer,
                root.GetProperty("globalRoles")
                    .EnumerateArray()
                    .Select(role => role.GetString()));
            Assert.False(root.TryGetProperty("characters", out _));

            var roles = root.GetProperty("campaigns")
                .EnumerateArray()
                .Select(campaign => campaign.GetProperty("role").GetString())
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "DM", "Player" }, roles);

            using var secondDelegation = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/{target.Slug}/upstream/api/second");
            secondDelegation.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", capability);
            using var secondDelegationResponse = await client.SendAsync(secondDelegation);
            Assert.Equal(HttpStatusCode.NoContent, secondDelegationResponse.StatusCode);
            var secondTargetTicket = capturingProxy.Calls[2].AuthenticationTicket;

            using var wrongTargetIntrospection = await IntrospectAsync(
                client,
                source.Slug,
                secondTargetTicket);
            Assert.Equal(HttpStatusCode.Unauthorized, wrongTargetIntrospection.StatusCode);

            using var cookieOnly = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/{target.Slug}/upstream/api/rules?userId={otherUserId:D}");
            cookieOnly.Headers.TryAddWithoutValidation(
                "Cookie",
                "__Host-dorks-and-dice.auth=browser-only");
            using var cookieOnlyResponse = await client.SendAsync(cookieOnly);
            Assert.Equal(HttpStatusCode.Unauthorized, cookieOnlyResponse.StatusCode);

            using var sourceMismatch = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/not-{source.Slug}/api/delegate/{target.Slug}/upstream/api/rules");
            sourceMismatch.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", capability);
            using var sourceMismatchResponse = await client.SendAsync(sourceMismatch);
            Assert.Equal(HttpStatusCode.Unauthorized, sourceMismatchResponse.StatusCode);

            using var unapproved = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/not-approved/upstream/api/rules");
            unapproved.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", capability);
            using var unapprovedResponse = await client.SendAsync(unapproved);
            Assert.Equal(HttpStatusCode.Forbidden, unapprovedResponse.StatusCode);
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, target);
        }
    }

    [Fact]
    public async Task ArbitraryToolCanNotDelegateToRulesCore()
    {
        var proxy = new CapturingToolProxyService();
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(proxy);
            });
        });

        var source = Tool(
            $"arbitrary-{Guid.NewGuid():N}",
            "http://arbitrary-tool:8080",
            [SiteModeValues.DorksAndDiceModeValue],
            delegationTargets: ["some-other-tool"]);
        var target = Tool(
            $"rules-core-{Guid.NewGuid():N}",
            "http://rules-core:8080",
            [SiteModeValues.DorksAndDiceModeValue]);
        await SaveToolsAsync(testFactory.Services, source, target);

        try
        {
            var userId = Guid.NewGuid();
            using var client = Client(testFactory);
            using var sourceRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/upstream/bootstrap");
            Authenticate(sourceRequest, userId, "Member");
            using var sourceResponse = await client.SendAsync(sourceRequest);
            Assert.Equal(HttpStatusCode.NoContent, sourceResponse.StatusCode);

            var sourceCall = Assert.Single(proxy.Calls);
            using var introspection = await IntrospectAsync(
                client,
                source.Slug,
                sourceCall.AuthenticationTicket);
            Assert.True(introspection.Headers.TryGetValues(
                ToolDelegationHeaders.Capability,
                out var capabilities));
            var capability = Assert.Single(capabilities);

            using var delegated = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/{target.Slug}/upstream/api/rules");
            delegated.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", capability);
            using var response = await client.SendAsync(delegated);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Single(proxy.Calls);
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, target);
        }
    }

    [Fact]
    public async Task DelegationEnforcesCapturedModeTargetStateContractAndUpstreamPolicy()
    {
        var source = Tool(
            CharacterToolContract.Slug,
            "http://character-sheet:8080",
            [SiteModeValues.DorksAndDiceModeValue],
            delegationTargets: ["rules-core"]);
        var target = Tool(
            "rules-core",
            "http://rules-core:8080",
            [SiteModeValues.DorksAndDiceModeValue]);
        await SaveToolsAsync(factory.Services, source, target);

        try
        {
            var userId = Guid.NewGuid();
            var sourceContext = new ToolHostAuthenticationContext
            {
                ToolSlug = source.Slug,
                SiteMode = SiteModeValues.DorksAndDiceModeValue,
                User = new ToolHostUserContext
                {
                    Id = userId.ToString("D"),
                    DisplayName = "Delegation Availability User"
                }
            };
            var capabilities = factory.Services.GetRequiredService<IToolDelegationCapabilityService>();
            var capability = capabilities.Issue(source.Slug, sourceContext);
            using var client = Client(factory, "dorks-and-dice-site");

            target.Enabled = false;
            await SaveToolsAsync(factory.Services, target);
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendDelegatedAsync(client, source.Slug, target.Slug, capability)).StatusCode);

            target.Enabled = true;
            target.Modes = [SiteModeValues.ProfessionalModeValue];
            await SaveToolsAsync(factory.Services, target);
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendDelegatedAsync(client, source.Slug, target.Slug, capability)).StatusCode);

            target.Modes = [SiteModeValues.DorksAndDiceModeValue];
            target.IntegrationContractVersion = 1;
            await SaveToolsAsync(factory.Services, target);
            Assert.Equal(
                HttpStatusCode.ServiceUnavailable,
                (await SendDelegatedAsync(client, source.Slug, target.Slug, capability)).StatusCode);

            target.IntegrationContractVersion =
                ToolIntegrationContractVersions.EmbeddedModuleCurrent;
            target.UpstreamBaseUrl = "https://delegation-target.example.test";
            await SaveToolsAsync(factory.Services, target);
            Assert.Equal(
                HttpStatusCode.BadGateway,
                (await SendDelegatedAsync(client, source.Slug, target.Slug, capability)).StatusCode);
        }
        finally
        {
            await DeleteToolsAsync(factory.Services, source, target);
        }
    }

    private static async Task<HttpResponseMessage> SendDelegatedAsync(
        HttpClient client,
        string sourceSlug,
        string targetSlug,
        string capability)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/tool-host/{sourceSlug}/api/delegate/{targetSlug}/upstream/api/rules");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", capability);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> IntrospectAsync(
        HttpClient client,
        string slug,
        string ticket)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/tool-host/{slug}/api/introspect");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", ticket);
        return await client.SendAsync(request);
    }

    private static void Authenticate(
        HttpRequestMessage request,
        Guid userId,
        string role)
    {
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, role);
        request.Headers.Add(
            TestRoleAuthenticationHandler.UserIdHeader,
            userId.ToString("D"));
    }

    private static ToolRegistration Tool(
        string slug,
        string upstream,
        IReadOnlyList<string> modes,
        IReadOnlyList<string>? delegationTargets = null) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        DisplayName = $"Delegation Test {slug}",
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion =
            ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = upstream,
        FrontendEntryPoint = "/app.js",
        Modes = modes.ToList(),
        DelegationTargets = delegationTargets?.ToList() ?? [],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task SaveToolsAsync(
        IServiceProvider services,
        params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            tool.UpdatedAt = DateTimeOffset.UtcNow;
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

    private static HttpClient Client(
        WebApplicationFactory<Program> host,
        string hostName = "dorks-and-dice.com") =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri($"https://{hostName}")
        });

    private sealed record CapturedProxyCall(
        string ToolSlug,
        string Path,
        string QueryString,
        string RequestBody,
        string AuthenticationTicket,
        string IntrospectionPath);

    private sealed class CapturingToolProxyService : IToolProxyService
    {
        public List<CapturedProxyCall> Calls { get; } = [];

        public Task ProxyAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }

        public async Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            string body;
            using (var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(cancellationToken);
            }

            Calls.Add(new CapturedProxyCall(
                tool.Slug,
                path,
                context.Request.QueryString.Value ?? string.Empty,
                body,
                authenticationTicket,
                introspectionPath));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        }
    }
}
