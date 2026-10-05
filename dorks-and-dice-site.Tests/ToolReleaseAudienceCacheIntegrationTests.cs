using System.Net;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolReleaseAudienceCacheIntegrationTests(
    PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task RestrictedAudienceDenialIsPrivateAndNoStore()
    {
        var tool = Application(ToolReleaseAudience.Development, allowAnonymous: false);
        await SaveAsync(tool);

        try
        {
            using var client = Client();
            using var request = Request($"/tools/{tool.Slug}");
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            AssertPrivateNoStore(response);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task AccountRequiredPublicChallengeIsPrivateAndNoStore()
    {
        var tool = Application(ToolReleaseAudience.Public, allowAnonymous: false);
        await SaveAsync(tool);

        try
        {
            using var client = Client();
            using var request = Request($"/tools/{tool.Slug}");
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertPrivateNoStore(response);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    private static ToolRegistration Application(
        ToolReleaseAudience audience,
        bool allowAnonymous)
    {
        var slug = $"release-cache-{Guid.NewGuid():N}";
        return new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = slug,
            Kind = ToolKind.Application,
            Slug = slug,
            DisplayName = "Release cache test",
            Modes = [BuiltInSiteModes.DorksAndDice.Id],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://release-cache-test:8080",
            FrontendEntryPoint = "/app.js",
            ReleaseAudience = audience,
            AllowAnonymous = allowAnonymous,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static HttpRequestMessage Request(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://dorks-and-dice.com{path}");
        request.Headers.Host = "dorks-and-dice.com";
        return request;
    }

    private static void AssertPrivateNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
    }

    private async Task SaveAsync(ToolRegistration tool)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
    }

    private async Task DeleteAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(id);
    }
}
