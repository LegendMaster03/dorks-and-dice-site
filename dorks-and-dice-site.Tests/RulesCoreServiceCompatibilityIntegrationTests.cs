using System.Net;
using System.Net.Http.Headers;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class RulesCoreServiceCompatibilityIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task LegacyRulesCoreUpstreamAliasTargetsHeadlessServiceByStableKey()
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

        ToolRegistration? previous;
        ToolRegistration service;
        using (var scope = testFactory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            previous = await registry.GetByKeyAsync("rules-core");
            service = new ToolRegistration
            {
                Id = previous?.Id ?? Guid.NewGuid(),
                Key = "rules-core",
                Kind = ToolKind.Service,
                Slug = null,
                DisplayName = "Rules Core",
                UpstreamBaseUrl = "http://localhost:8124",
                HealthPath = "/ready",
                Modes = [SiteModeValues.DorksAndDiceModeValue],
                AllowAnonymous = false,
                Enabled = true,
                CreatedAt = previous?.CreatedAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await registry.SaveAsync(service);
        }

        try
        {
            var userId = Guid.NewGuid();
            using var client = Client(testFactory);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/tool-host/rules-core/api/upstream/api/rules/monster.red-dragon?scope=global");
            Authenticate(request, userId, "Member");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var call = Assert.Single(proxy.Calls);
            Assert.Equal("rules-core", call.ToolKey);
            Assert.Null(call.ToolSlug);
            Assert.Equal(ToolKind.Service, call.Kind);
            Assert.Equal("/api/rules/monster.red-dragon", call.Path);
            Assert.Equal(
                "/tool-host/registrations/rules-core/api/introspect",
                call.IntrospectionPath);

            using var introspection = new HttpRequestMessage(
                HttpMethod.Post,
                "/tool-host/registrations/rules-core/api/introspect");
            introspection.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                call.AuthenticationTicket);
            using var introspectionResponse = await client.SendAsync(introspection);
            Assert.Equal(HttpStatusCode.OK, introspectionResponse.StatusCode);

            using var publicRoute = await client.GetAsync("/tools/rules-core");
            Assert.Equal(HttpStatusCode.NotFound, publicRoute.StatusCode);
        }
        finally
        {
            using var scope = testFactory.Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            if (previous is null)
            {
                await registry.DeleteAsync(service.Id);
            }
            else
            {
                await registry.SaveAsync(previous);
            }
        }
    }

    private static void Authenticate(HttpRequestMessage request, Guid userId, string role)
    {
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, role);
        request.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString("D"));
    }

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

    private sealed record CapturedProxyCall(
        string ToolKey,
        string? ToolSlug,
        ToolKind Kind,
        string Path,
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

        public Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CapturedProxyCall(
                tool.Key,
                tool.Slug,
                tool.Kind,
                path,
                authenticationTicket,
                introspectionPath));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }
}
