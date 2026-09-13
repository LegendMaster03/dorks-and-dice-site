using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class ToolIntegrationContractPolicyTests
{
    [Fact]
    public void CurrentEmbeddedModuleContractIsAccepted()
    {
        var tool = Embedded("current", ToolIntegrationContractVersions.EmbeddedModuleCurrent);

        Assert.True(ToolIntegrationContractPolicy.IsSupported(tool));
        Assert.Null(ToolIntegrationContractPolicy.GetUnsupportedReason(tool));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(3)]
    public void MissingOrUnsupportedEmbeddedModuleContractIsRejected(int? version)
    {
        var tool = Embedded("legacy", version);

        Assert.False(ToolIntegrationContractPolicy.IsSupported(tool));
        var error = Assert.IsType<string>(ToolIntegrationContractPolicy.GetUnsupportedReason(tool));
        Assert.Contains("Supported version is 2", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("block-initiative")]
    [InlineData("rules-core")]
    public void FirstPartyEmbeddedModuleRegistrationsRemainValid(string slug)
    {
        var tool = Embedded(slug, ToolIntegrationContractVersions.EmbeddedModuleCurrent);

        Assert.True(ToolIntegrationContractPolicy.IsSupported(tool));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(99)]
    public void ProxiedApplicationsAreNotSubjectToEmbeddedModuleContract(int? version)
    {
        var tool = new ToolRegistration
        {
            Slug = "proxied",
            IntegrationType = ToolIntegrationType.ProxiedApplication,
            IntegrationContractVersion = version
        };

        Assert.True(ToolIntegrationContractPolicy.IsSupported(tool));
    }

    private static ToolRegistration Embedded(string slug, int? version) => new()
    {
        Slug = slug,
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = version
    };
}

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class EmbeddedModuleContractIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task UnsupportedEmbeddedModuleContractFailsClearlyAcrossHostingBoundaries()
    {
        var tool = await RegisterAsync(new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"legacy-contract-{Guid.NewGuid():N}",
            DisplayName = "Legacy Contract Tool",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = 1,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = true,
            Enabled = true
        });

        try
        {
            using var client = Client(factory);
            foreach (var path in new[]
            {
                $"/tools/{tool.Slug}",
                $"/tool-modules/{tool.Slug}/app.js",
                $"/tool-host/{tool.Slug}/context",
                $"/tool-host/{tool.Slug}/api/upstream/api/preview"
            })
            {
                using var response = await client.GetAsync(path);
                var body = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Contains("Unsupported tool integration contract", body, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Supported version is 2", body, StringComparison.Ordinal);
            }
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task DevPortalRejectsUnsupportedEmbeddedModuleContractWithUsefulValidation()
    {
        using var client = Client(factory, "localhost");
        client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Dev");

        var html = await client.GetStringAsync("/development/tools/new");
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success);

        using var response = await client.PostAsync(
            "/development/tools/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
                ["Slug"] = $"unsupported-contract-{Guid.NewGuid():N}",
                ["DisplayName"] = "Unsupported Contract",
                ["IntegrationType"] = ((int)ToolIntegrationType.EmbeddedModule).ToString(),
                ["IntegrationContractVersion"] = "1",
                ["Modes"] = SiteModeValues.DorksAndDiceModeValue,
                ["AllowAnonymous"] = "true"
            }));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Supported version is 2", body, StringComparison.Ordinal);
    }

    private async Task<ToolRegistration> RegisterAsync(ToolRegistration tool)
    {
        tool.CreatedAt = DateTimeOffset.UtcNow;
        tool.UpdatedAt = tool.CreatedAt;
        using var scope = factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        return tool;
    }

    private async Task DeleteAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.DeleteAsync(id);
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, string hostName = "dorks-and-dice.com") =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri($"https://{hostName}")
        });
}
