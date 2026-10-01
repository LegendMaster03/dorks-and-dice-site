using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class HeadlessToolServiceBrowserProxyIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task StableKeyBrowserGatewayTargetsHeadlessServiceAndUsesStableIntrospection()
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

        var service = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = $"browser-service-{Guid.NewGuid():N}",
            Kind = ToolKind.Service,
            Slug = null,
            DisplayName = "Browser service fixture",
            UpstreamBaseUrl = "http://localhost:8124",
            HealthPath = "/ready",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await SaveAsync(testFactory.Services, service);

        try
        {
            using var client = testFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://dorks-and-dice.com")
            });
            var userId = Guid.NewGuid();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/registrations/{service.Key}/api/upstream/api/rules?entityType=monster&q=goblin&limit=8");
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            request.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString("D"));

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var call = Assert.Single(proxy.Calls);
            Assert.Equal(service.Key, call.ToolKey);
            Assert.Null(call.ToolSlug);
            Assert.Equal("/api/rules", call.Path);
            Assert.Equal(
                $"/tool-host/registrations/{service.Key}/api/introspect",
                call.IntrospectionPath);

            using var introspectionRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/tool-host/registrations/{service.Key}/api/introspect");
            introspectionRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", call.AuthenticationTicket);
            using var introspection = await client.SendAsync(introspectionRequest);
            Assert.Equal(HttpStatusCode.OK, introspection.StatusCode);

            using var json = JsonDocument.Parse(await introspection.Content.ReadAsStringAsync());
            Assert.Equal(service.Key, json.RootElement.GetProperty("toolKey").GetString());
            Assert.False(json.RootElement.TryGetProperty("toolSlug", out _));
            Assert.Equal(
                userId.ToString("D"),
                json.RootElement.GetProperty("user").GetProperty("id").GetString());
        }
        finally
        {
            await DeleteAsync(testFactory.Services, service.Id);
        }
    }

    [Fact]
    public void RulesCoreLegacyBrowserRouteRemainsAsExplicitCompatibilityAlias()
    {
        var method = typeof(ToolServiceApiController).GetMethod(
            nameof(ToolServiceApiController.LegacyRulesCoreUpstream),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);

        var routes = method!
            .GetCustomAttributes<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();
        Assert.Contains("/tool-host/rules-core/api/upstream", routes);
        Assert.Contains("/tool-host/rules-core/api/upstream/{**proxyPath}", routes);
    }

    private static async Task SaveAsync(IServiceProvider services, ToolRegistration tool)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
    }

    private static async Task DeleteAsync(IServiceProvider services, Guid toolId)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(toolId);
    }

    private sealed record CapturedProxyCall(
        string ToolKey,
        string? ToolSlug,
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
                path,
                authenticationTicket,
                introspectionPath));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }
}
