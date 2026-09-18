using System.Net;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolNavigationIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task AnonymousDorksNavigationAndToolsIndexShowOnlyAnonymousVisibleTools()
    {
        var anonymousTool = await RegisterAsync(
            "Anonymous Dorks Tool",
            SiteModeValues.DorksAndDiceModeValue,
            allowAnonymous: true);
        var authenticatedTool = await RegisterAsync(
            "Authenticated Dorks Tool",
            SiteModeValues.DorksAndDiceModeValue,
            allowAnonymous: false);
        var professionalTool = await RegisterAsync(
            "Professional Tool",
            SiteModeValues.ProfessionalModeValue,
            allowAnonymous: true);

        try
        {
            using var client = CreateClient("https://dorks-and-dice.com");

            using var home = await client.GetAsync("/");
            var homeHtml = await home.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, home.StatusCode);
            Assert.Contains("data-site-tools-navigation", homeHtml, StringComparison.Ordinal);
            Assert.Contains($"/tools/{anonymousTool.Slug}", homeHtml, StringComparison.Ordinal);
            Assert.DoesNotContain($"/tools/{authenticatedTool.Slug}", homeHtml, StringComparison.Ordinal);
            Assert.DoesNotContain($"/tools/{professionalTool.Slug}", homeHtml, StringComparison.Ordinal);

            using var index = await client.GetAsync("/tools");
            var indexHtml = await index.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Contains(anonymousTool.DisplayName, indexHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(authenticatedTool.DisplayName, indexHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(professionalTool.DisplayName, indexHtml, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteAsync(anonymousTool, authenticatedTool, professionalTool);
        }
    }

    [Fact]
    public async Task AuthenticatedDorksNavigationShowsAnonymousAndSignedInTools()
    {
        var anonymousTool = await RegisterAsync(
            "Anonymous Dorks Tool",
            SiteModeValues.DorksAndDiceModeValue,
            allowAnonymous: true);
        var authenticatedTool = await RegisterAsync(
            "Authenticated Dorks Tool",
            SiteModeValues.DorksAndDiceModeValue,
            allowAnonymous: false);

        try
        {
            using var client = CreateClient("https://dorks-and-dice.com");
            client.DefaultRequestHeaders.Add(
                TestRoleAuthenticationHandler.RolesHeader,
                AccountRoles.GlobalEditor);

            using var home = await client.GetAsync("/");
            var html = await home.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, home.StatusCode);
            Assert.Contains("data-site-tools-navigation", html, StringComparison.Ordinal);
            Assert.Contains($"/tools/{anonymousTool.Slug}", html, StringComparison.Ordinal);
            Assert.Contains($"/tools/{authenticatedTool.Slug}", html, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteAsync(anonymousTool, authenticatedTool);
        }
    }

    [Fact]
    public async Task ProfessionalNavigationDoesNotRenderToolsDropdown()
    {
        var professionalTool = await RegisterAsync(
            "Professional Navigation Tool",
            SiteModeValues.ProfessionalModeValue,
            allowAnonymous: true);

        try
        {
            using var client = CreateClient("https://kylebarnett.com");

            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Host = "kylebarnett.com";
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("data-site-tools-navigation", html, StringComparison.Ordinal);
            Assert.DoesNotContain($"/tools/{professionalTool.Slug}", html, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteAsync(professionalTool);
        }
    }

    private HttpClient CreateClient(string baseAddress) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(baseAddress)
        });

    private async Task<ToolRegistration> RegisterAsync(
        string displayName,
        string mode,
        bool allowAnonymous)
    {
        var registry = factory.Services.GetRequiredService<IToolRegistry>();
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"nav-{Guid.NewGuid():N}",
            DisplayName = $"{displayName} {Guid.NewGuid():N}",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            FrontendEntryPoint = "/app.js",
            Modes = [mode],
            AllowAnonymous = allowAnonymous,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await registry.SaveAsync(tool);
        return tool;
    }

    private async Task DeleteAsync(params ToolRegistration[] tools)
    {
        var registry = factory.Services.GetRequiredService<IToolRegistry>();
        foreach (var tool in tools)
        {
            await registry.DeleteAsync(tool.Id);
        }
    }
}
