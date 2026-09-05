using System.Net;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostContextIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ToolHostContextIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousToolReturnsSafeAnonymousContext()
    {
        var tool = await RegisterToolAsync(allowAnonymous: true, SiteModeValues.DorksAndDiceModeValue);
        try
        {
            using var response = await SendAsync("dorks-and-dice.com", $"/tool-host/{tool.Slug}/context");
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, json.RootElement.GetProperty("contractVersion").GetInt32());
            Assert.Equal(tool.Slug, json.RootElement.GetProperty("toolSlug").GetString());
            Assert.Equal(SiteModeValues.DorksAndDiceModeValue, json.RootElement.GetProperty("siteMode").GetString());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("user").ValueKind);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        finally
        {
            await DeleteToolAsync(tool.Id);
        }
    }

    [Fact]
    public async Task AuthenticatedRequestReturnsOnlyStableUserSummary()
    {
        var tool = await RegisterToolAsync(allowAnonymous: false, SiteModeValues.DorksAndDiceModeValue);
        try
        {
            using var request = CreateRequest("dorks-and-dice.com", $"/tool-host/{tool.Slug}/context");
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, "Member");
            using var response = await SendAsync(request);
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var user = json.RootElement.GetProperty("user");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("integration-test-user", user.GetProperty("id").GetString());
            Assert.Equal("Integration Test User", user.GetProperty("displayName").GetString());
            Assert.False(user.TryGetProperty("email", out _));
            Assert.False(json.RootElement.TryGetProperty("roles", out _));
        }
        finally
        {
            await DeleteToolAsync(tool.Id);
        }
    }

    [Fact]
    public async Task AccountRequiredToolChallengesAnonymousContextRequest()
    {
        var tool = await RegisterToolAsync(allowAnonymous: false, SiteModeValues.DorksAndDiceModeValue);
        try
        {
            using var response = await SendAsync("dorks-and-dice.com", $"/tool-host/{tool.Slug}/context");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await DeleteToolAsync(tool.Id);
        }
    }

    [Fact]
    public async Task ContextEndpointHonorsToolModeVisibility()
    {
        var tool = await RegisterToolAsync(allowAnonymous: true, SiteModeValues.ProfessionalModeValue);
        try
        {
            using var response = await SendAsync("dorks-and-dice.com", $"/tool-host/{tool.Slug}/context");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await DeleteToolAsync(tool.Id);
        }
    }

    private async Task<ToolRegistration> RegisterToolAsync(bool allowAnonymous, string mode)
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"context-{Guid.NewGuid():N}",
            DisplayName = "Context Test Tool",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            Modes = [mode],
            AllowAnonymous = allowAnonymous,
            Enabled = true
        };

        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        return tool;
    }

    private async Task DeleteToolAsync(Guid id)
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
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return request;
    }
}
