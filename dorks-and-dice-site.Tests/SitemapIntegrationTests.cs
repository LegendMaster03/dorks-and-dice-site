using System.Net;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class SitemapIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public SitemapIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SitemapIncludesCurrentDatabaseBackedPublicContent()
    {
        var response = await SendAsync("kylebarnett.com", "/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("http://kylebarnett.com/articles/freeing-the-bees-consolevariations-puzzle", xml);
        Assert.Contains("http://kylebarnett.com/resume/personalmultimodewebsite", xml);
        Assert.Contains("http://kylebarnett.com/resume/experiencecybersecurityteam?context=experience", xml);
        Assert.DoesNotContain("/development", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SitemapKeepsIntrinsicModeRoutesAlongsideDatabaseContent()
    {
        var response = await SendAsync("kylebarnett.com", "/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<loc>http://kylebarnett.com/</loc>", xml);
        Assert.Contains("<loc>http://kylebarnett.com/articles</loc>", xml);
    }

    [Fact]
    public async Task SitemapIncludesOnlyAnonymousPublicToolsForActiveMode()
    {
        var publicTool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug("public"),
            DisplayName = "Public Sitemap Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            ReleaseAudience = ToolReleaseAudience.Public,
            AllowAnonymous = true,
            Enabled = true
        });
        var accountTool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug("account"),
            DisplayName = "Account Sitemap Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            ReleaseAudience = ToolReleaseAudience.Public,
            AllowAnonymous = false,
            Enabled = true
        });
        var testingTool = await RegisterAsync(new ToolRegistration
        {
            Slug = UniqueSlug("testing"),
            DisplayName = "Testing Sitemap Tool",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            ReleaseAudience = ToolReleaseAudience.Testing,
            AllowAnonymous = false,
            Enabled = true
        });

        try
        {
            var dorksResponse = await SendAsync("dorks-and-dice.com", "/sitemap.xml");
            var dorksXml = await dorksResponse.Content.ReadAsStringAsync();
            var professionalResponse = await SendAsync("kylebarnett.com", "/sitemap.xml");
            var professionalXml = await professionalResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, dorksResponse.StatusCode);
            Assert.Contains("<loc>http://dorks-and-dice.com/tools</loc>", dorksXml);
            Assert.Contains($"<loc>http://dorks-and-dice.com/tools/{publicTool.Slug}</loc>", dorksXml);
            Assert.DoesNotContain(accountTool.Slug!, dorksXml, StringComparison.Ordinal);
            Assert.DoesNotContain(testingTool.Slug!, dorksXml, StringComparison.Ordinal);
            Assert.DoesNotContain(publicTool.Slug!, professionalXml, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteAsync(publicTool.Id);
            await DeleteAsync(accountTool.Id);
            await DeleteAsync(testingTool.Id);
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
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}{path}");
        request.Headers.Host = host;
        return await client.SendAsync(request);
    }

    private static string UniqueSlug(string suffix) => $"sitemap-{suffix}-{Guid.NewGuid():N}";
}
