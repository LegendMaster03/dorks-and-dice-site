using System.Net;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class SiteTextIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public SiteTextIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SiteTextUsesThePublicProfessionalCatalog()
    {
        var response = await SendAsync("/site.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", text);
        Assert.Contains("http://kylebarnett.com/articles/freeing-the-bees-consolevariations-puzzle", text);
        Assert.Contains("Personal Multi-Mode Website", text);
        Assert.DoesNotContain("_internal:unlisted", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/development", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmsTextProvidesACompactPublicIndex()
    {
        var response = await SendAsync("/llms.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Full public text: http://kylebarnett.com/site.txt", text);
        Assert.Contains("Freeing the Bees: Solving ConsoleVariations", text);
        Assert.DoesNotContain("## Freeing the Bees\n\n- Inspect the console", text);
    }

    [Fact]
    public async Task PublicToolsAppearInSiteAndLlmsTextWithoutLeakingTestingTools()
    {
        var publicTool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug("public"),
            DisplayName = "Public Text Tool",
            Description = "A public tabletop utility for text discovery testing.",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            ReleaseAudience = ToolReleaseAudience.Public,
            AllowAnonymous = true,
            Enabled = true
        });
        var testingTool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug("testing"),
            DisplayName = "Testing Text Tool",
            Description = "This must not appear in public text discovery.",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://localhost:8123",
            FrontendEntryPoint = "/app.js",
            ReleaseAudience = ToolReleaseAudience.Testing,
            AllowAnonymous = false,
            Enabled = true
        });

        try
        {
            var siteResponse = await SendAsync("dorks-and-dice.com", "/site.txt");
            var siteText = await siteResponse.Content.ReadAsStringAsync();
            var llmsResponse = await SendAsync("dorks-and-dice.com", "/llms.txt");
            var llmsText = await llmsResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, siteResponse.StatusCode);
            Assert.Contains("## Public Text Tool", siteText);
            Assert.Contains($"URL: http://dorks-and-dice.com/tools/{publicTool.Slug}", siteText);
            Assert.Contains("Type: Interactive Tool", siteText);
            Assert.Contains(publicTool.Description!, siteText);
            Assert.DoesNotContain(testingTool.DisplayName, siteText, StringComparison.Ordinal);
            Assert.DoesNotContain(testingTool.Slug!, siteText, StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.OK, llmsResponse.StatusCode);
            Assert.Contains($"- Public Text Tool: http://dorks-and-dice.com/tools/{publicTool.Slug}", llmsText);
            Assert.Contains(publicTool.Description!, llmsText);
            Assert.DoesNotContain(testingTool.DisplayName, llmsText, StringComparison.Ordinal);
            Assert.DoesNotContain(testingTool.Slug!, llmsText, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteAsync(publicTool.Id);
            await DeleteAsync(testingTool.Id);
        }
    }

    private Task<HttpResponseMessage> SendAsync(string path) =>
        SendAsync("kylebarnett.com", path);

    private async Task<HttpResponseMessage> SendAsync(string host, string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        return await client.SendAsync(request);
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

    private static string UniqueSlug(string suffix) => $"site-text-{suffix}-{Guid.NewGuid():N}";
}
