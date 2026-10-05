using System.Net;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
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
public sealed class ToolReleaseAudienceExecutionIntegrationTests(
    PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task DevelopmentToolHiddenFromTesterCanNotBeUsedThroughKnownDirectBoundaries()
    {
        var tool = EmbeddedTool(ToolReleaseAudience.Development, allowAnonymous: false);
        await SaveAsync(factory, tool);

        try
        {
            using var client = Client(factory);
            var scopedTester = $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Tester}";

            using (var listingRequest = Request(
                       HttpMethod.Get,
                       "dorks-and-dice.com",
                       "/tools",
                       scopedRoles: scopedTester))
            using (var listing = await client.SendAsync(listingRequest))
            {
                var html = await listing.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
                Assert.DoesNotContain($"/tools/{tool.Slug}", html, StringComparison.Ordinal);
            }

            foreach (var path in ExecutionPaths(tool))
            {
                using var request = Request(
                    HttpMethod.Get,
                    "dorks-and-dice.com",
                    path,
                    scopedRoles: scopedTester);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
        finally
        {
            await DeleteAsync(factory, tool.Id);
        }
    }

    [Fact]
    public async Task RestrictiveAudienceWinsEvenWhenAllowAnonymousIsTrue()
    {
        foreach (var audience in new[]
                 {
                     ToolReleaseAudience.Development,
                     ToolReleaseAudience.Testing
                 })
        {
            var tool = EmbeddedTool(audience, allowAnonymous: true);
            await SaveAsync(factory, tool);
            try
            {
                using var client = Client(factory);
                foreach (var path in ExecutionPaths(tool))
                {
                    using var request = Request(HttpMethod.Get, "dorks-and-dice.com", path);
                    using var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }
            }
            finally
            {
                await DeleteAsync(factory, tool.Id);
            }
        }
    }

    [Fact]
    public async Task TestingAudienceIsModeScopedAndDevRetainsInheritedTesterAuthority()
    {
        var tool = EmbeddedTool(ToolReleaseAudience.Testing, allowAnonymous: false);
        await SaveAsync(factory, tool);

        try
        {
            using var client = Client(factory);
            using var anonymousRequest = Request(HttpMethod.Get, "dorks-and-dice.com", $"/tools/{tool.Slug}");
            using var anonymous = await client.SendAsync(anonymousRequest);
            Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);

            using var memberRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{tool.Slug}",
                roles: "Member");
            using var member = await client.SendAsync(memberRequest);
            Assert.Equal(HttpStatusCode.NotFound, member.StatusCode);

            using var wrongModeRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{tool.Slug}",
                scopedRoles: $"{BuiltInSiteModes.Professional.Id}:{ScopedAccountRoles.Tester}");
            using var wrongMode = await client.SendAsync(wrongModeRequest);
            Assert.Equal(HttpStatusCode.NotFound, wrongMode.StatusCode);

            using var testerRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{tool.Slug}",
                scopedRoles: $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Tester}");
            using var tester = await client.SendAsync(testerRequest);
            Assert.Equal(HttpStatusCode.OK, tester.StatusCode);
            AssertPrivateNoStore(tester);

            using var devRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{tool.Slug}",
                roles: AccountRoles.Dev);
            using var dev = await client.SendAsync(devRequest);
            Assert.Equal(HttpStatusCode.OK, dev.StatusCode);
            AssertPrivateNoStore(dev);
        }
        finally
        {
            await DeleteAsync(factory, tool.Id);
        }
    }

    [Fact]
    public async Task PublicAudienceRetainsAnonymousAndAccountRequiredBehavior()
    {
        var anonymousTool = EmbeddedTool(ToolReleaseAudience.Public, allowAnonymous: true);
        var accountTool = EmbeddedTool(ToolReleaseAudience.Public, allowAnonymous: false);
        await SaveAsync(factory, anonymousTool);
        await SaveAsync(factory, accountTool);

        try
        {
            using var client = Client(factory);

            using var publicRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{anonymousTool.Slug}");
            using var publicResponse = await client.SendAsync(publicRequest);
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

            using var accountAnonymousRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{accountTool.Slug}");
            using var accountAnonymous = await client.SendAsync(accountAnonymousRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, accountAnonymous.StatusCode);

            using var accountMemberRequest = Request(
                HttpMethod.Get,
                "dorks-and-dice.com",
                $"/tools/{accountTool.Slug}",
                roles: "Member");
            using var accountMember = await client.SendAsync(accountMemberRequest);
            Assert.Equal(HttpStatusCode.OK, accountMember.StatusCode);
            AssertPrivateNoStore(accountMember);
        }
        finally
        {
            await DeleteAsync(factory, anonymousTool.Id);
            await DeleteAsync(factory, accountTool.Id);
        }
    }

    [Fact]
    public async Task RestrictedProxiedApplicationDoesNotReachUpstreamForUnauthorizedForwardedMethods()
    {
        var proxy = new RecordingToolProxyService();
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(proxy);
            });
        });
        var tool = ProxiedTool(ToolReleaseAudience.Development);
        await SaveAsync(testFactory, tool);

        try
        {
            using var client = Client(testFactory);
            var scopedTester = $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Tester}";
            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Delete })
            {
                using var request = Request(
                    method,
                    "dorks-and-dice.com",
                    $"/tools/{tool.Slug}/nested/path",
                    scopedRoles: scopedTester);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            Assert.Empty(proxy.Calls);

            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Delete })
            {
                using var request = Request(
                    method,
                    "localhost",
                    $"/tools/{tool.Slug}/nested/path",
                    roles: AccountRoles.Dev,
                    trustedPreview: true);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                AssertPrivateNoStore(response);
            }
            Assert.Equal(3, proxy.Calls.Count);
        }
        finally
        {
            await DeleteAsync(testFactory, tool.Id);
        }
    }

    [Fact]
    public async Task DelegatedApplicationTargetRequiresInitiatingUsersDestinationAudience()
    {
        var target = EmbeddedTool(ToolReleaseAudience.Development, allowAnonymous: false);
        using var scope = factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IToolHostAuthenticationContextFactory>();
        var testerContext = HostContext(scopedRoles: [ScopedAccountRoles.Tester]);
        var devContext = HostContext(
            globalRoles: [AccountRoles.Dev],
            scopedRoles: [ScopedAccountRoles.Tester]);

        Assert.Null(await contextFactory.CreateDelegatedAsync(target, testerContext));
        Assert.NotNull(await contextFactory.CreateDelegatedAsync(target, devContext));
        Assert.Null(await contextFactory.CreatePrivateTunnelAsync(target, testerContext));
        Assert.NotNull(await contextFactory.CreatePrivateTunnelAsync(target, devContext));
    }

    private static IReadOnlyList<string> ExecutionPaths(ToolRegistration tool) =>
    [
        $"/tools/{tool.Slug}",
        $"/tools/{tool.Slug}/nested/path",
        $"/tool-host/{tool.Slug}/context",
        $"/tool-modules/{tool.Slug}/app.js",
        $"/tool-host/{tool.Slug}/api/upstream/api/test"
    ];

    private static ToolRegistration EmbeddedTool(
        ToolReleaseAudience audience,
        bool allowAnonymous)
    {
        var slug = $"release-exec-{Guid.NewGuid():N}";
        return new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = slug,
            Slug = slug,
            DisplayName = "Release execution test",
            Modes = [BuiltInSiteModes.DorksAndDice.Id],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://release-execution-test:8080",
            FrontendEntryPoint = "/app.js",
            ReleaseAudience = audience,
            AllowAnonymous = allowAnonymous,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ToolRegistration ProxiedTool(ToolReleaseAudience audience)
    {
        var tool = EmbeddedTool(audience, allowAnonymous: false);
        tool.IntegrationType = ToolIntegrationType.ProxiedApplication;
        tool.IntegrationContractVersion = null;
        tool.FrontendEntryPoint = null;
        return tool;
    }

    private static ToolHostAuthenticationContext HostContext(
        IReadOnlyList<string>? globalRoles = null,
        IReadOnlyList<string>? scopedRoles = null) => new()
    {
        ToolKey = "source-tool",
        ToolSlug = "source-tool",
        SiteMode = BuiltInSiteModes.DorksAndDice.Id,
        User = new ToolHostUserContext
        {
            Id = TestRoleAuthenticationHandler.DefaultUserId,
            DisplayName = "Release integration user"
        },
        GlobalRoles = globalRoles ?? [],
        ScopedRoles = scopedRoles ?? []
    };

    private static HttpClient Client(WebApplicationFactory<Program> appFactory) =>
        appFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static HttpRequestMessage Request(
        HttpMethod method,
        string host,
        string path,
        string? roles = null,
        string? scopedRoles = null,
        bool trustedPreview = false)
    {
        var request = new HttpRequestMessage(method, $"http://{host}{path}");
        request.Headers.Host = host;
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, roles);
        }
        if (!string.IsNullOrWhiteSpace(scopedRoles))
        {
            request.Headers.Add(TestRoleAuthenticationHandler.ScopedRolesHeader, scopedRoles);
        }
        if (trustedPreview)
        {
            request.Headers.Add(
                "Cookie",
                $"{SiteModeValues.DevelopmentSiteModeCookie}={BuiltInSiteModes.DorksAndDice.Id}");
        }
        return request;
    }

    private static void AssertPrivateNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
    }

    private static async Task SaveAsync(
        WebApplicationFactory<Program> appFactory,
        ToolRegistration tool)
    {
        using var scope = appFactory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
    }

    private static async Task DeleteAsync(
        WebApplicationFactory<Program> appFactory,
        Guid id)
    {
        using var scope = appFactory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(id);
    }

    private sealed class RecordingToolProxyService : IToolProxyService
    {
        public List<(string Method, string ToolKey, string Path)> Calls { get; } = [];

        public Task ProxyAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((context.Request.Method, tool.Key, path));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }

        public Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default) =>
            ProxyAsync(context, tool, path, cancellationToken);
    }
}
