using dorks_and_dice_site.Controllers;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Tests;

public sealed class RulesWikiCompatibilityRedirectTests
{
    [Fact]
    public async Task LegacyRulesCoreDeepLinkRedirectsToRulesWikiAfterCoreBecomesHeadless()
    {
        var registry = new StubToolRegistry(
            Service("rules-core"),
            Application("rules-wiki"));
        var controller = Controller(registry, HttpMethods.Get, "?scope=global&tab=detail");

        var result = await controller.RoutedRequest(
            "rules-core",
            "monsters/ancient-red-dragon",
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(
            "/tools/rules-wiki/monsters/ancient-red-dragon?scope=global&tab=detail",
            redirect.Url);
        Assert.True(redirect.PreserveMethod);
        Assert.False(redirect.Permanent);
    }

    [Fact]
    public async Task ExistingRulesCoreApplicationTakesPrecedenceBeforeCutover()
    {
        var rulesCore = Application("rules-core");
        var registry = new StubToolRegistry(rulesCore, Application("rules-wiki"));
        var controller = Controller(registry, HttpMethods.Get);

        var result = await controller.Details("rules-core", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Details", view.ViewName);
        Assert.Same(rulesCore, view.Model);
    }

    [Fact]
    public async Task LegacyRulesCoreRouteRemainsNotFoundUntilRulesWikiIsAvailable()
    {
        var rulesWiki = Application("rules-wiki");
        rulesWiki.Enabled = false;
        var registry = new StubToolRegistry(Service("rules-core"), rulesWiki);
        var controller = Controller(registry, HttpMethods.Get);

        var result = await controller.RoutedRequest(
            "rules-core",
            "spells/fireball",
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LegacyCompatibilityDoesNotRedirectNonBrowserMutationRequests()
    {
        var registry = new StubToolRegistry(
            Service("rules-core"),
            Application("rules-wiki"));
        var controller = Controller(registry, HttpMethods.Post);

        var result = await controller.RoutedRequest(
            "rules-core",
            "api/global/rules/publish",
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static ToolsController Controller(
        IToolRegistry registry,
        string method,
        string? queryString = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            httpContext.Request.QueryString = new QueryString(queryString);
        }
        httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.DorksAndDice
        };

        return new ToolsController(registry, new NoopToolProxyService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static ToolRegistration Application(string slug) => new()
    {
        Id = Guid.NewGuid(),
        Key = slug,
        Kind = ToolKind.Application,
        Slug = slug,
        DisplayName = slug,
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = $"http://{slug}:8080",
        FrontendEntryPoint = "/app.js",
        Modes = [SiteModeValues.DorksAndDiceModeValue],
        AllowAnonymous = true,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static ToolRegistration Service(string key) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Kind = ToolKind.Service,
        Slug = null,
        DisplayName = key,
        IntegrationType = null,
        IntegrationContractVersion = null,
        UpstreamBaseUrl = $"http://{key}:8080",
        FrontendEntryPoint = null,
        Modes = [SiteModeValues.DorksAndDiceModeValue],
        AllowAnonymous = false,
        Enabled = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class StubToolRegistry(params ToolRegistration[] tools) : IToolRegistry
    {
        private readonly List<ToolRegistration> _tools = [.. tools];

        public Task<IReadOnlyList<ToolRegistration>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolRegistration>>(_tools);

        public Task<ToolRegistration?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tools.FirstOrDefault(tool => tool.Id == id));

        public Task<ToolRegistration?> GetByKeyAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tools.FirstOrDefault(tool => string.Equals(
                tool.Key,
                key,
                StringComparison.OrdinalIgnoreCase)));

        public Task<ToolRegistration?> GetBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tools.FirstOrDefault(tool => string.Equals(
                tool.Slug,
                slug,
                StringComparison.OrdinalIgnoreCase)));

        public Task SaveAsync(
            ToolRegistration registration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoopToolProxyService : IToolProxyService
    {
        public Task ProxyAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
