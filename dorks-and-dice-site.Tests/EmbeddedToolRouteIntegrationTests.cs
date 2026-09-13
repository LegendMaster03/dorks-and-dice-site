using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class EmbeddedToolRouteIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task EmbeddedRootAndNestedRoutesRenderHostShellWithRouteContext()
    {
        var tool = await RegisterAsync();
        try
        {
            using var client = Client(factory);

            using var root = await client.GetAsync($"/tools/{tool.Slug}");
            var rootHtml = await root.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, root.StatusCode);
            Assert.Contains($"data-tool-base-path=\"/tools/{tool.Slug}\"", rootHtml);
            Assert.Contains("data-tool-route=\"/\"", rootHtml);
            Assert.Contains($"data-tool-context-url=\"/tool-host/{tool.Slug}/context\"", rootHtml);

            using var nested = await client.GetAsync($"/tools/{tool.Slug}/monsters/ancient-red-dragon?sort=name");
            var nestedHtml = await nested.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, nested.StatusCode);
            Assert.Contains($"data-tool-base-path=\"/tools/{tool.Slug}\"", nestedHtml);
            Assert.Contains("data-tool-route=\"/monsters/ancient-red-dragon\"", nestedHtml);
            Assert.Contains(
                $"data-tool-context-url=\"/tool-host/{tool.Slug}/context?toolRoute=%2Fmonsters%2Fancient-red-dragon\"",
                nestedHtml);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task EmbeddedNestedRouteContextSupportsDirectRefresh()
    {
        var tool = await RegisterAsync();
        try
        {
            using var client = Client(factory);
            using var page = await client.GetAsync($"/tools/{tool.Slug}/spells/fireball");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);

            using var context = await client.GetAsync(
                $"/tool-host/{tool.Slug}/context?toolRoute=%2Fspells%2Ffireball");
            using var json = JsonDocument.Parse(await context.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            Assert.Equal($"/tools/{tool.Slug}", json.RootElement.GetProperty("toolBasePath").GetString());
            Assert.Equal("/spells/fireball", json.RootElement.GetProperty("toolRoute").GetString());
            Assert.Equal($"/tool-host/{tool.Slug}/api", json.RootElement.GetProperty("apiBaseUrl").GetString());
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task UnknownDisabledAndWrongModeNestedRoutesReturnNotFound()
    {
        using var client = Client(factory);
        using var unknown = await client.GetAsync($"/tools/missing-{Guid.NewGuid():N}/monsters");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var disabled = await RegisterAsync(enabled: false);
        var wrongMode = await RegisterAsync(modes: [SiteModeValues.ProfessionalModeValue]);
        try
        {
            using var disabledResponse = await client.GetAsync($"/tools/{disabled.Slug}/monsters");
            using var wrongModeResponse = await client.GetAsync($"/tools/{wrongMode.Slug}/monsters");
            Assert.Equal(HttpStatusCode.NotFound, disabledResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, wrongModeResponse.StatusCode);
        }
        finally
        {
            await DeleteAsync(disabled.Id);
            await DeleteAsync(wrongMode.Id);
        }
    }

    [Fact]
    public async Task AccountRequiredEmbeddedNestedRouteChallengesAnonymous()
    {
        var tool = await RegisterAsync(allowAnonymous: false);
        try
        {
            using var client = Client(factory);
            using var anonymous = await client.GetAsync($"/tools/{tool.Slug}/monsters");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            client.DefaultRequestHeaders.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            using var authenticated = await client.GetAsync($"/tools/{tool.Slug}/monsters");
            Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task ProxiedApplicationStillOwnsNestedRouteResponses()
    {
        var requests = new List<Uri>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(ToolHttpClientNames.Proxy).ConfigurePrimaryHttpMessageHandler(() =>
                new RecordingHandler(request =>
                {
                    requests.Add(request.RequestUri!);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("proxied")
                    };
                }))));
        var tool = await RegisterAsync(
            integrationType: ToolIntegrationType.ProxiedApplication,
            upstreamBaseUrl: "http://route-test:8080/base");
        try
        {
            using var client = Client(host);
            using var response = await client.GetAsync($"/tools/{tool.Slug}/monsters/dragon?q=red");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("proxied", await response.Content.ReadAsStringAsync());
            var upstream = Assert.Single(requests);
            Assert.Equal("/base/monsters/dragon", upstream.AbsolutePath);
            Assert.Equal("?q=red", upstream.Query);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    private async Task<ToolRegistration> RegisterAsync(
        bool enabled = true,
        bool allowAnonymous = true,
        List<string>? modes = null,
        ToolIntegrationType integrationType = ToolIntegrationType.EmbeddedModule,
        string? upstreamBaseUrl = null)
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"route-{Guid.NewGuid():N}",
            DisplayName = "Route Test Tool",
            IntegrationType = integrationType,
            UpstreamBaseUrl = upstreamBaseUrl,
            Modes = modes ?? [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = allowAnonymous,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

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

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
