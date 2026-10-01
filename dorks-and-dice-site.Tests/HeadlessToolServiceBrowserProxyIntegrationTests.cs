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
    public async Task RulesCoreStableKeyBrowserGatewayUsesStableServiceIntrospection()
    {
        var proxy = new CapturingToolProxyService();
        var service = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = "rules-core",
            Kind = ToolKind.Service,
            Slug = null,
            DisplayName = "Rules Core",
            UpstreamBaseUrl = "http://localhost:8124",
            HealthPath = "/ready",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolRegistry>();
                services.AddSingleton<IToolRegistry>(new FixedToolRegistry(service));
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(proxy);
            });
        });

        using var client = testFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        var userId = Guid.NewGuid();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/tool-host/registrations/rules-core/api/upstream/api/rules?entityType=monster&q=goblin&limit=8");
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
        request.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString("D"));

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var call = Assert.Single(proxy.Calls);
        Assert.Equal(service.Key, call.ToolKey);
        Assert.Null(call.ToolSlug);
        Assert.Equal("/api/rules", call.Path);
        Assert.Equal(
            "/tool-host/registrations/rules-core/api/introspect",
            call.IntrospectionPath);

        using var introspectionRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/tool-host/registrations/rules-core/api/introspect");
        introspectionRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", call.AuthenticationTicket);
        using var introspection = await client.SendAsync(introspectionRequest);
        Assert.Equal(HttpStatusCode.OK, introspection.StatusCode);

        using var json = JsonDocument.Parse(await introspection.Content.ReadAsStringAsync());
        Assert.Equal("rules-core", json.RootElement.GetProperty("toolKey").GetString());
        Assert.False(json.RootElement.TryGetProperty("toolSlug", out _));
        Assert.Equal(
            userId.ToString("D"),
            json.RootElement.GetProperty("user").GetProperty("id").GetString());
    }

    [Fact]
    public void RulesCoreStableRouteIsExplicitRatherThanGenericServiceExposure()
    {
        var stableMethod = typeof(ToolServiceApiController).GetMethod(
            nameof(ToolServiceApiController.RulesCoreRegistrationUpstream),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(stableMethod);
        var stableRoutes = stableMethod!
            .GetCustomAttributes<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();
        Assert.Contains("/tool-host/registrations/rules-core/api/upstream", stableRoutes);
        Assert.Contains("/tool-host/registrations/rules-core/api/upstream/{**proxyPath}", stableRoutes);
        Assert.DoesNotContain(stableRoutes, route => route?.Contains("{registrationKey}", StringComparison.Ordinal) == true);

        var allRoutes = typeof(ToolServiceApiController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetCustomAttributes<RouteAttribute>())
            .Select(attribute => attribute.Template)
            .Where(route => route is not null)
            .ToArray();
        Assert.DoesNotContain("/tool-host/rules-core/api/upstream", allRoutes);
        Assert.DoesNotContain("/tool-host/rules-core/api/upstream/{**proxyPath}", allRoutes);
    }

    private sealed class FixedToolRegistry(ToolRegistration tool) : IToolRegistry
    {
        public Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolRegistration>>([tool]);

        public Task<ToolRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolRegistration?>(id == tool.Id ? tool : null);

        public Task<ToolRegistration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolRegistration?>(
                string.Equals(key, tool.Key, StringComparison.OrdinalIgnoreCase) ? tool : null);

        public Task<ToolRegistration?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolRegistration?>(null);

        public Task SaveAsync(ToolRegistration registration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
