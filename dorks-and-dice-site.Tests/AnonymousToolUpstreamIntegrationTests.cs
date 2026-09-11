using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class AnonymousToolUpstreamIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public AnonymousToolUpstreamIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousEmbeddedToolCanReachUnauthenticatedUpstreamGateway()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Anonymous Embedded Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            AllowAnonymous = true,
            Enabled = true
        });

        try
        {
            var response = await SendAsync(
                "dorks-and-dice.com",
                $"/tool-host/{tool.Slug}/api/upstream/api/preview");

            // The test upstream intentionally does not exist. BadGateway proves the request
            // passed host authorization and reached the proxy instead of being challenged.
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task AccountRequiredEmbeddedToolStillChallengesAnonymousUpstreamRequest()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Account Embedded Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            AllowAnonymous = false,
            Enabled = true
        });

        try
        {
            var response = await SendAsync(
                "dorks-and-dice.com",
                $"/tool-host/{tool.Slug}/api/upstream/api/preview");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task ProxiedApplicationCanNotUseEmbeddedUpstreamGateway()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug(),
            DisplayName = "Proxied Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.ProxiedApplication,
            UpstreamBaseUrl = "http://localhost:8123",
            AllowAnonymous = true,
            Enabled = true
        });

        try
        {
            var response = await SendAsync(
                "dorks-and-dice.com",
                $"/tool-host/{tool.Slug}/api/upstream/api/preview");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return await client.SendAsync(request);
    }

    private static string UniqueSlug() => $"anonymous-tool-{Guid.NewGuid():N}";
}
