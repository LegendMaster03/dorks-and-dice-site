using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class ToolHostingRuntimeTests
{
    [Fact]
    public void ToolVisibilityHonorsSelectedModes()
    {
        var tool = new ToolRegistration
        {
            Modes = [SiteModeValues.ProfessionalModeValue]
        };

        Assert.True(ToolVisibility.IsVisibleInMode(tool, SiteMode.Professional));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.DorksAndDice));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.Unassigned));
    }

    [Fact]
    public void LegacyToolWithoutModesRemainsDorksAndDiceOnly()
    {
        var tool = new ToolRegistration
        {
            Modes = []
        };

        Assert.True(ToolVisibility.IsVisibleInMode(tool, SiteMode.DorksAndDice));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.Professional));
    }

    [Theory]
    [InlineData("/tools/test-tool")]
    [InlineData("/tool-modules/test-tool/app.js")]
    [InlineData("/tool-host/test-tool/context")]
    public void ToolRoutesAreModeAdaptive(string path)
    {
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.DorksAndDice));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Professional));
        Assert.False(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Unassigned));
    }

    [Theory]
    [InlineData("http://localhost:8123")]
    [InlineData("http://initiative:8080")]
    [InlineData("https://reference-data")]
    public void UpstreamPolicyAllowsLoopbackAndSingleLabelServices(string upstream)
    {
        var policy = CreatePolicy();

        Assert.True(policy.IsAllowed(upstream, out var reason), reason);
    }

    [Theory]
    [InlineData("https://google.com")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://10.0.0.7:8080")]
    [InlineData("ftp://initiative:21")]
    [InlineData("http://user:password@initiative:8080")]
    public void UpstreamPolicyRejectsUnapprovedExternalOrLiteralHosts(string upstream)
    {
        var policy = CreatePolicy();

        Assert.False(policy.IsAllowed(upstream, out _));
    }

    [Fact]
    public void UpstreamPolicyAllowsExplicitlyConfiguredFqdnOrIp()
    {
        var policy = CreatePolicy(new Dictionary<string, string?>
        {
            ["ToolHosting:AllowedUpstreamHosts:0"] = "tools.internal.example",
            ["ToolHosting:AllowedUpstreamHosts:1"] = "10.0.0.7"
        });

        Assert.True(policy.IsAllowed("https://tools.internal.example:8443", out var fqdnReason), fqdnReason);
        Assert.True(policy.IsAllowed("http://10.0.0.7:8080", out var ipReason), ipReason);
    }

    [Fact]
    public void UpstreamUriRejectsDecodedPathTraversal()
    {
        var tool = new ToolRegistration
        {
            UpstreamBaseUrl = "http://initiative:8080"
        };

        Assert.False(ToolUpstreamUri.TryBuild(tool, "/%2e%2e/secrets", QueryString.Empty, out _));
    }

    [Fact]
    public void UpstreamUriPreservesSafeQueryString()
    {
        var tool = new ToolRegistration
        {
            UpstreamBaseUrl = "http://initiative:8080"
        };

        Assert.True(ToolUpstreamUri.TryBuild(tool, "/app.js", new QueryString("?v=123"), out var uri));
        Assert.Equal("http://initiative:8080/app.js?v=123", uri?.ToString());
    }

    [Theory]
    [InlineData("/..\\secrets")]
    [InlineData("/%2e%2e%5csecrets")]
    [InlineData("/%252e%252e/secrets")]
    [InlineData("/folder/%252e%252e%252fsecrets")]
    public void UpstreamUriRejectsAmbiguousTraversal(string path)
    {
        var tool = new ToolRegistration { UpstreamBaseUrl = "http://initiative:8080/private-tool" };
        Assert.False(ToolUpstreamUri.TryBuild(tool, path, QueryString.Empty, out _));
    }

    private static ToolUpstreamPolicy CreatePolicy(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        return new ToolUpstreamPolicy(configuration);
    }
}

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostingIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ToolHostingIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ToolDetailsHonorModeSelection()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Professional Tool",
            Modes = [SiteModeValues.ProfessionalModeValue],
            Enabled = true
        });

        try
        {
            var professional = await SendAsync("kylebarnett.com", $"/tools/{tool.Slug}");
            var dorks = await SendAsync("dorks-and-dice.com", $"/tools/{tool.Slug}");

            Assert.Equal(HttpStatusCode.OK, professional.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, dorks.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task DisabledToolAndModuleRoutesReturnNotFound()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Disabled Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            Enabled = false
        });

        try
        {
            var details = await SendAsync("dorks-and-dice.com", $"/tools/{tool.Slug}");
            var module = await SendAsync("dorks-and-dice.com", $"/tool-modules/{tool.Slug}/app.js");

            Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, module.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task AccountRequiredModuleChallengesAnonymousBeforeUpstreamResolution()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Account Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            AllowAnonymous = false,
            Enabled = true
        });

        try
        {
            var anonymous = await SendAsync("dorks-and-dice.com", $"/tool-modules/{tool.Slug}/app.js");
            using var authenticatedRequest = CreateRequest("dorks-and-dice.com", $"/tool-modules/{tool.Slug}/app.js");
            authenticatedRequest.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            var authenticated = await SendAsync(authenticatedRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Equal(HttpStatusCode.BadGateway, authenticated.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    private async Task<ToolRegistration> RegisterAsync(ToolRegistration tool)
    {
        tool.Id = Guid.NewGuid();
        tool.CreatedAt = DateTimeOffset.UtcNow;
        tool.UpdatedAt = tool.CreatedAt;
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        return tool;
    }

    private async Task DeleteAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.DeleteAsync(id);
    }

    private async Task<HttpResponseMessage> SendAsync(string host, string path)
    {
        using var request = CreateRequest(host, path);
        return await SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        return request;
    }

    private static string UniqueSlug() => $"tool-{Guid.NewGuid():N}";
}
