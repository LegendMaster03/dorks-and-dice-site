using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class HeadlessToolServiceIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task ServiceRegistrationHasNoPublicToolSurfaceButRemainsVisibleToDevelopmentAdministration()
    {
        var key = $"headless-{Guid.NewGuid():N}";
        var service = Service(key);
        await SaveToolsAsync(factory.Services, service);

        try
        {
            using var publicClient = Client(factory);
            using var toolsResponse = await publicClient.GetAsync("/tools");
            var toolsHtml = await toolsResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
            Assert.DoesNotContain(service.DisplayName, toolsHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(service.Key, toolsHtml, StringComparison.Ordinal);

            using var publicRoute = await publicClient.GetAsync($"/tools/{service.Key}");
            Assert.Equal(HttpStatusCode.NotFound, publicRoute.StatusCode);

            using var adminClient = Client(factory, "localhost");
            adminClient.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Dev");
            var adminHtml = await adminClient.GetStringAsync("/development/tools");
            Assert.Contains(service.DisplayName, adminHtml, StringComparison.Ordinal);
            Assert.Contains(service.Key, adminHtml, StringComparison.Ordinal);
            Assert.Contains("Service", adminHtml, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteToolsAsync(factory.Services, service);
        }
    }

    [Fact]
    public async Task DevelopmentPortalCreatesAndEditsServiceWithoutApplicationOnlyFields()
    {
        var key = $"admin-service-{Guid.NewGuid():N}";
        using var client = Client(factory, "localhost");
        client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Dev");

        var createHtml = await client.GetStringAsync("/development/tools/new");
        var createToken = AntiforgeryToken(createHtml);
        using var createResponse = await client.PostAsync(
            "/development/tools/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = createToken,
                ["Key"] = key,
                ["Kind"] = ((int)ToolKind.Service).ToString(),
                ["DisplayName"] = "Headless Admin Service",
                ["Description"] = "Created without a public slug or hosting contract.",
                ["UpstreamBaseUrl"] = "http://localhost:8124",
                ["HealthPath"] = "/ready",
                ["Modes"] = SiteModeValues.DorksAndDiceModeValue,
                ["Enabled"] = "true"
            }));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        ToolRegistration stored;
        using (var scope = factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            stored = Assert.IsType<ToolRegistration>(await registry.GetByKeyAsync(key));
            Assert.Equal(ToolKind.Service, stored.Kind);
            Assert.Null(stored.Slug);
            Assert.Null(stored.IntegrationType);
            Assert.Null(stored.IntegrationContractVersion);
            Assert.Null(stored.FrontendEntryPoint);
            Assert.True(stored.Enabled);
        }

        try
        {
            var editHtml = await client.GetStringAsync($"/development/tools/{stored.Id:D}");
            Assert.Contains(stored.Key, editHtml, StringComparison.Ordinal);
            Assert.Contains("Headless service", editHtml, StringComparison.OrdinalIgnoreCase);
            var editToken = AntiforgeryToken(editHtml);

            using var editResponse = await client.PostAsync(
                "/development/tools/save",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = editToken,
                    ["Id"] = stored.Id.ToString("D"),
                    ["Key"] = key,
                    ["Kind"] = ((int)ToolKind.Service).ToString(),
                    ["DisplayName"] = "Updated Headless Admin Service",
                    ["UpstreamBaseUrl"] = "http://localhost:8124",
                    ["HealthPath"] = "/ready",
                    ["Modes"] = SiteModeValues.DorksAndDiceModeValue
                }));
            Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

            using var scope = factory.Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            var updated = Assert.IsType<ToolRegistration>(await registry.GetByKeyAsync(key));
            Assert.Equal("Updated Headless Admin Service", updated.DisplayName);
            Assert.False(updated.Enabled);
            Assert.Null(updated.Slug);
            Assert.Null(updated.IntegrationType);
        }
        finally
        {
            await DeleteToolsAsync(factory.Services, stored);
        }
    }

    [Fact]
    public async Task DelegationTargetsHeadlessServiceByStableKeyAndIssuesTargetScopedAuthentication()
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

        var serviceKey = $"rules-service-{Guid.NewGuid():N}";
        var source = Application(
            $"source-{Guid.NewGuid():N}",
            delegationTargets: [serviceKey]);
        var target = Service(serviceKey);
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
            Assert.Equal(source.Key, sourceCall.ToolKey);
            Assert.Equal(source.Slug, sourceCall.ToolSlug);
            Assert.Equal($"/tool-host/{source.Slug}/api/introspect", sourceCall.IntrospectionPath);

            using var sourceIntrospection = await IntrospectApplicationAsync(
                client,
                source.Slug!,
                sourceCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, sourceIntrospection.StatusCode);
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

            using var delegated = DelegatedRequest(source.Slug!, target.Key, capability, "/api/rules");
            using var delegatedResponse = await client.SendAsync(delegated);
            Assert.Equal(HttpStatusCode.NoContent, delegatedResponse.StatusCode);

            Assert.Equal(2, proxy.Calls.Count);
            var targetCall = proxy.Calls[1];
            Assert.Equal(target.Key, targetCall.ToolKey);
            Assert.Null(targetCall.ToolSlug);
            Assert.Equal("/api/rules", targetCall.Path);
            Assert.Equal(
                $"/tool-host/registrations/{target.Key}/api/introspect",
                targetCall.IntrospectionPath);
            Assert.NotEqual(sourceCall.AuthenticationTicket, targetCall.AuthenticationTicket);

            using var targetIntrospection = await IntrospectRegistrationAsync(
                client,
                target.Key,
                targetCall.AuthenticationTicket);
            Assert.Equal(HttpStatusCode.OK, targetIntrospection.StatusCode);
            using (var json = JsonDocument.Parse(await targetIntrospection.Content.ReadAsStringAsync()))
            {
                Assert.Equal(target.Key, json.RootElement.GetProperty("toolKey").GetString());
                Assert.False(json.RootElement.TryGetProperty("toolSlug", out _));
                Assert.Equal(userId.ToString("D"), json.RootElement.GetProperty("user").GetProperty("id").GetString());
            }

            using var secondDelegated = DelegatedRequest(source.Slug!, target.Key, capability, "/api/second");
            using var secondResponse = await client.SendAsync(secondDelegated);
            Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);
            var secondTargetTicket = proxy.Calls[2].AuthenticationTicket;

            using var wrongScope = await IntrospectApplicationAsync(client, source.Slug!, secondTargetTicket);
            Assert.Equal(HttpStatusCode.Unauthorized, wrongScope.StatusCode);

            using var sourceTicketRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/tool-host/{source.Slug}/api/upstream/another");
            Authenticate(sourceTicketRequest, userId, "Member");
            using var sourceTicketResponse = await client.SendAsync(sourceTicketRequest);
            Assert.Equal(HttpStatusCode.NoContent, sourceTicketResponse.StatusCode);
            var ordinarySourceTicket = proxy.Calls[3].AuthenticationTicket;

            using var ticketAsCapability = DelegatedRequest(
                source.Slug!,
                target.Key,
                ordinarySourceTicket,
                "/api/rejected");
            using var ticketAsCapabilityResponse = await client.SendAsync(ticketAsCapability);
            Assert.Equal(HttpStatusCode.Unauthorized, ticketAsCapabilityResponse.StatusCode);

            using var unapproved = DelegatedRequest(
                source.Slug!,
                $"not-approved-{Guid.NewGuid():N}",
                capability,
                "/api/rules");
            using var unapprovedResponse = await client.SendAsync(unapproved);
            Assert.Equal(HttpStatusCode.Forbidden, unapprovedResponse.StatusCode);

            target.Enabled = false;
            await SaveToolsAsync(testFactory.Services, target);
            using var disabled = DelegatedRequest(source.Slug!, target.Key, capability, "/api/rules");
            using var disabledResponse = await client.SendAsync(disabled);
            Assert.Equal(HttpStatusCode.NotFound, disabledResponse.StatusCode);
        }
        finally
        {
            await DeleteToolsAsync(testFactory.Services, source, target);
        }
    }

    [Fact]
    public async Task ServiceHealthCheckUsesConfiguredUpstreamWithoutPublicSlug()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = new HttpRequestMessage(request.Method, request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var factory = new FixedHttpClientFactory(new HttpClient(handler));
        var policy = new ToolUpstreamPolicy(new ConfigurationBuilder().Build());
        var health = new ToolHealthService(factory, policy);
        var service = Service($"health-{Guid.NewGuid():N}");
        service.UpstreamBaseUrl = "http://localhost:8125";
        service.HealthPath = "/ready";

        var result = await health.CheckAsync(service);

        Assert.Equal(ToolHealthStatus.Healthy, result.Status);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(new Uri("http://localhost:8125/ready"), captured!.RequestUri);
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static ToolRegistration Application(
        string slug,
        IReadOnlyList<string>? delegationTargets = null) => new()
    {
        Id = Guid.NewGuid(),
        Key = slug,
        Kind = ToolKind.Application,
        Slug = slug,
        DisplayName = $"Application {slug}",
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = "http://localhost:8123",
        FrontendEntryPoint = "/app.js",
        Modes = [SiteModeValues.DorksAndDiceModeValue],
        DelegationTargets = delegationTargets?.ToList() ?? [],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static ToolRegistration Service(string key) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Kind = ToolKind.Service,
        Slug = null,
        DisplayName = $"Service {key}",
        IntegrationType = null,
        IntegrationContractVersion = null,
        UpstreamBaseUrl = "http://localhost:8124",
        FrontendEntryPoint = null,
        HealthPath = "/ready",
        Modes = [SiteModeValues.DorksAndDiceModeValue],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static HttpRequestMessage DelegatedRequest(
        string sourceSlug,
        string targetKey,
        string credential,
        string path)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/tool-host/{sourceSlug}/api/delegate/{targetKey}/upstream{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        return request;
    }

    private static async Task<HttpResponseMessage> IntrospectApplicationAsync(
        HttpClient client,
        string slug,
        string ticket)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/tool-host/{slug}/api/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> IntrospectRegistrationAsync(
        HttpClient client,
        string key,
        string ticket)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/tool-host/registrations/{key}/api/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        return await client.SendAsync(request);
    }

    private static void Authenticate(HttpRequestMessage request, Guid userId, string role)
    {
        request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, role);
        request.Headers.Add(TestRoleAuthenticationHandler.UserIdHeader, userId.ToString("D"));
    }

    private static async Task SaveToolsAsync(IServiceProvider services, params ToolRegistration[] tools)
    {
        using var scope = services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            tool.UpdatedAt = DateTimeOffset.UtcNow;
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

    private static HttpClient Client(WebApplicationFactory<Program> host, string hostName = "dorks-and-dice.com") =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri($"https://{hostName}")
        });

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

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }
}

public sealed class HeadlessToolServiceMigrationTests
{
    [Fact]
    public async Task LegacySqliteRegistrationMigratesStableKeyWithoutChangingPublicSlugOrData()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"headless-tool-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE tool_registration (
                        tool_id TEXT NOT NULL PRIMARY KEY,
                        tool_slug TEXT NOT NULL,
                        tool_display_name TEXT NOT NULL,
                        tool_description TEXT NULL,
                        tool_integration_type INTEGER NOT NULL DEFAULT 0,
                        tool_integration_contract_version INTEGER NULL,
                        tool_upstream_base_url TEXT NULL,
                        tool_frontend_entry_point TEXT NULL,
                        tool_health_path TEXT NULL,
                        tool_modes TEXT NOT NULL DEFAULT '[]',
                        tool_delegation_targets TEXT NOT NULL DEFAULT '[]',
                        tool_allow_anonymous INTEGER NOT NULL DEFAULT 1,
                        tool_enabled INTEGER NOT NULL DEFAULT 0,
                        tool_created_at TEXT NOT NULL,
                        tool_updated_at TEXT NOT NULL);
                    CREATE UNIQUE INDEX ux_tool_registration_slug_ci
                        ON tool_registration(lower(tool_slug));
                    INSERT INTO tool_registration (
                        tool_id, tool_slug, tool_display_name, tool_description,
                        tool_integration_type, tool_integration_contract_version,
                        tool_upstream_base_url, tool_frontend_entry_point, tool_health_path,
                        tool_modes, tool_delegation_targets, tool_allow_anonymous, tool_enabled,
                        tool_created_at, tool_updated_at)
                    VALUES (
                        '11111111-1111-1111-1111-111111111111',
                        'legacy-app',
                        'Legacy Application',
                        'Preserve me',
                        0,
                        2,
                        'http://legacy-app:8080',
                        '/app.js',
                        '/ready',
                        '["dorks-and-dice"]',
                        '["rules-core"]',
                        0,
                        1,
                        '2026-09-01T00:00:00.0000000+00:00',
                        '2026-09-02T00:00:00.0000000+00:00');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var sourceRegistry = CreateSourceRegistry(databasePath);
            await new ContentStorageInitializer(sourceRegistry).InitializeAsync();
            var registry = new DatabaseToolRegistry(sourceRegistry);

            var byKey = Assert.IsType<ToolRegistration>(await registry.GetByKeyAsync("LEGACY-APP"));
            var bySlug = Assert.IsType<ToolRegistration>(await registry.GetBySlugAsync("LEGACY-APP"));

            Assert.Equal(byKey.Id, bySlug.Id);
            Assert.Equal("legacy-app", byKey.Key);
            Assert.Equal("legacy-app", byKey.Slug);
            Assert.Equal(ToolKind.Application, byKey.Kind);
            Assert.Equal("Legacy Application", byKey.DisplayName);
            Assert.Equal("Preserve me", byKey.Description);
            Assert.Equal(ToolIntegrationType.EmbeddedModule, byKey.IntegrationType);
            Assert.Equal(ToolIntegrationContractVersions.EmbeddedModuleCurrent, byKey.IntegrationContractVersion);
            Assert.Equal("http://legacy-app:8080", byKey.UpstreamBaseUrl);
            Assert.Equal("/app.js", byKey.FrontendEntryPoint);
            Assert.Equal("/ready", byKey.HealthPath);
            Assert.Equal(new[] { SiteModeValues.DorksAndDiceModeValue }, byKey.Modes);
            Assert.Equal(new[] { "rules-core" }, byKey.DelegationTargets);
            Assert.False(byKey.AllowAnonymous);
            Assert.True(byKey.Enabled);
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete(databasePath + "-shm");
            TryDelete(databasePath + "-wal");
        }
    }

    private static ContentSourceRegistry CreateSourceRegistry(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HeadlessMigration"] = $"Data Source={databasePath};Pooling=False",
                ["ContentStorage:AuthoringSource"] = "HeadlessMigration",
                ["ContentStorage:Sources:HeadlessMigration:DisplayName"] = "Headless migration",
                ["ContentStorage:Sources:HeadlessMigration:Provider"] = "Sqlite",
                ["ContentStorage:Sources:HeadlessMigration:ConnectionString"] = "HeadlessMigration"
            })
            .Build();
        return new ContentSourceRegistry(configuration, AppContext.BaseDirectory);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
