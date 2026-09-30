using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class DelegatedCallerIdentityIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task DelegatedTargetContextIdentifiesImmediateSourceToolButDirectContextDoesNot()
    {
        var userId = Guid.NewGuid();
        var proxy = new CapturingToolProxyService();
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(proxy);
            });
        });

        var source = Tool("rules-wiki", "http://rules-wiki:8080", delegationTargets: ["rules-core"]);
        var target = Tool("rules-core", "http://rules-core:8080");
        await SaveToolsAsync(testFactory.Services, source, target);

        try
        {
            using var client = testFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://dorks-and-dice.com")
            });

            using var sourceRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/upstream/bootstrap");
            Authenticate(sourceRequest, userId);
            using var sourceResponse = await client.SendAsync(sourceRequest);
            Assert.Equal(HttpStatusCode.NoContent, sourceResponse.StatusCode);

            var sourceCall = Assert.Single(proxy.Calls);
            using var sourceIntrospection = await IntrospectAsync(
                client,
                source.Slug!,
                sourceCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, sourceIntrospection.StatusCode);
            var capability = Assert.Single(sourceIntrospection.Headers.GetValues(ToolDelegationHeaders.Capability));

            using var delegatedRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/delegate/{target.Slug}/upstream/internal/wiki/references");
            delegatedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", capability);
            using var delegatedResponse = await client.SendAsync(delegatedRequest);
            Assert.Equal(HttpStatusCode.NoContent, delegatedResponse.StatusCode);

            var targetCall = proxy.Calls[1];
            using var targetIntrospection = await IntrospectAsync(
                client,
                target.Slug!,
                targetCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, targetIntrospection.StatusCode);

            using var delegatedJson = JsonDocument.Parse(await targetIntrospection.Content.ReadAsStringAsync());
            var delegatedRoot = delegatedJson.RootElement;
            Assert.Equal(target.Key, delegatedRoot.GetProperty("toolKey").GetString());
            Assert.Equal(source.Key, delegatedRoot.GetProperty("delegatedFromToolKey").GetString());
            Assert.Equal(source.Slug, delegatedRoot.GetProperty("delegatedFromToolSlug").GetString());

            using var directTargetRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{target.Slug}/api/upstream/api/rules");
            Authenticate(directTargetRequest, userId);
            using var directTargetResponse = await client.SendAsync(directTargetRequest);
            Assert.Equal(HttpStatusCode.NoContent, directTargetResponse.StatusCode);

            var directTargetCall = proxy.Calls[2];
            using var directTargetIntrospection = await IntrospectAsync(
                client,
                target.Slug!,
                directTargetCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, directTargetIntrospection.StatusCode);
            using var directJson = JsonDocument.Parse(await directTargetIntrospection.Content.ReadAsStringAsync());
            Assert.False(directJson.RootElement.TryGetProperty("delegatedFromToolKey", out _));
            Assert.False(directJson.RootElement.TryGetProperty("delegatedFromToolSlug", out _));
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, target);
        }
    }

    private static ToolRegistration Tool(
        string slug,
        string upstream,
        IReadOnlyList<string>? delegationTargets = null) => new()
    {
        Id = Guid.NewGuid(),
        Key = slug,
        Slug = slug,
        DisplayName = $"Delegated caller test {slug}",
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = upstream,
        FrontendEntryPoint = "/app.js",
        Modes = [SiteModeValues.DorksAndDiceModeValue],
        DelegationTargets = delegationTargets?.ToList() ?? [],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static void Authenticate(HttpRequestMessage request, Guid userId)
    {
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, AccountRoles.Owner);
        request.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString("D"));
    }

    private static async Task<HttpResponseMessage> IntrospectAsync(HttpClient client, string slug, string ticket)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/tool-host/{slug}/api/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        return await client.SendAsync(request);
    }

    private static async Task SaveToolsAsync(IServiceProvider services, params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            await registry.SaveAsync(tool);
        }
    }

    private static async Task DeleteToolsAsync(IServiceProvider services, params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    private sealed record CapturedCall(
        string ToolKey,
        string Path,
        string AuthenticationTicket,
        string IntrospectionPath);

    private sealed class CapturingToolProxyService : IToolProxyService
    {
        public List<CapturedCall> Calls { get; } = [];

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
            Calls.Add(new CapturedCall(tool.Key, path, authenticationTicket, introspectionPath));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }
}
