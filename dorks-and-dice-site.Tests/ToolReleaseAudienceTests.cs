using System.Net;
using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class ToolReleaseAudienceTests
{
    [Fact]
    public void TestingAudienceUsesModeScopedTesterAuthority()
    {
        var tool = Tool(ToolReleaseAudience.Testing);
        var dorksTester = ScopedPrincipal(BuiltInSiteModes.DorksAndDice.Id, ScopedAccountRoles.Tester);
        var professionalTester = ScopedPrincipal(BuiltInSiteModes.Professional.Id, ScopedAccountRoles.Tester);
        var dev = GlobalPrincipal(AccountRoles.Dev);

        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, dorksTester));
        Assert.False(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, professionalTester));
        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, dev));
    }

    [Fact]
    public void DevelopmentAudienceRequiresDevAuthority()
    {
        var tool = Tool(ToolReleaseAudience.Development);
        var tester = ScopedPrincipal(BuiltInSiteModes.DorksAndDice.Id, ScopedAccountRoles.Tester);
        var dev = GlobalPrincipal(AccountRoles.Dev);

        Assert.False(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, tester));
        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, dev));
    }

    [Fact]
    public void PublicAudienceRetainsAnonymousAndAccountRequiredBehavior()
    {
        var tool = Tool(ToolReleaseAudience.Public);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var member = AuthenticatedPrincipal();

        tool.AllowAnonymous = true;
        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, anonymous));

        tool.FrontendEntryPoint = null;
        Assert.False(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, anonymous));
        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, member));
        tool.FrontendEntryPoint = "/app.js";

        tool.AllowAnonymous = false;
        Assert.False(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, anonymous));
        Assert.True(ToolVisibility.IsVisibleToUser(tool, BuiltInSiteModes.DorksAndDice.Id, member));
    }

    [Fact]
    public void ApplicationAccessPolicyGivesReleaseAudiencePrecedenceOverAllowAnonymous()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var tester = ScopedPrincipal(BuiltInSiteModes.DorksAndDice.Id, ScopedAccountRoles.Tester);
        var member = AuthenticatedPrincipal();
        var dev = GlobalPrincipal(AccountRoles.Dev);
        var development = Tool(ToolReleaseAudience.Development);
        development.AllowAnonymous = true;
        var testing = Tool(ToolReleaseAudience.Testing);
        testing.AllowAnonymous = true;
        var publicAccountRequired = Tool(ToolReleaseAudience.Public);
        publicAccountRequired.AllowAnonymous = false;

        Assert.Equal(
            ToolApplicationAccessDecision.ReleaseAudienceDenied,
            ToolApplicationAccessPolicy.Evaluate(development, BuiltInSiteModes.DorksAndDice.Id, anonymous));
        Assert.Equal(
            ToolApplicationAccessDecision.ReleaseAudienceDenied,
            ToolApplicationAccessPolicy.Evaluate(development, BuiltInSiteModes.DorksAndDice.Id, tester));
        Assert.Equal(
            ToolApplicationAccessDecision.Allowed,
            ToolApplicationAccessPolicy.Evaluate(development, BuiltInSiteModes.DorksAndDice.Id, dev));

        Assert.Equal(
            ToolApplicationAccessDecision.ReleaseAudienceDenied,
            ToolApplicationAccessPolicy.Evaluate(testing, BuiltInSiteModes.DorksAndDice.Id, anonymous));
        Assert.Equal(
            ToolApplicationAccessDecision.Allowed,
            ToolApplicationAccessPolicy.Evaluate(testing, BuiltInSiteModes.DorksAndDice.Id, tester));
        Assert.Equal(
            ToolApplicationAccessDecision.Allowed,
            ToolApplicationAccessPolicy.Evaluate(testing, BuiltInSiteModes.DorksAndDice.Id, dev));

        Assert.Equal(
            ToolApplicationAccessDecision.AuthenticationRequired,
            ToolApplicationAccessPolicy.Evaluate(publicAccountRequired, BuiltInSiteModes.DorksAndDice.Id, anonymous));
        Assert.Equal(
            ToolApplicationAccessDecision.Allowed,
            ToolApplicationAccessPolicy.Evaluate(publicAccountRequired, BuiltInSiteModes.DorksAndDice.Id, member));
    }

    [Fact]
    public void ApplicationAccessPolicyRequiresBothModeAndTestingAuthority()
    {
        var tool = Tool(ToolReleaseAudience.Testing);
        tool.Modes = [BuiltInSiteModes.Professional.Id];
        var dorksTester = ScopedPrincipal(BuiltInSiteModes.DorksAndDice.Id, ScopedAccountRoles.Tester);
        var professionalTester = ScopedPrincipal(BuiltInSiteModes.Professional.Id, ScopedAccountRoles.Tester);

        Assert.Equal(
            ToolApplicationAccessDecision.Unavailable,
            ToolApplicationAccessPolicy.Evaluate(tool, BuiltInSiteModes.DorksAndDice.Id, dorksTester));
        Assert.Equal(
            ToolApplicationAccessDecision.Allowed,
            ToolApplicationAccessPolicy.Evaluate(tool, BuiltInSiteModes.Professional.Id, professionalTester));
    }

    [Fact]
    public void DelegatedApplicationTargetUsesInitiatingUserReleaseAuthority()
    {
        var development = Tool(ToolReleaseAudience.Development);
        var testing = Tool(ToolReleaseAudience.Testing);
        var testerContext = HostContext(scopedRoles: [ScopedAccountRoles.Tester]);
        var devContext = HostContext(globalRoles: [AccountRoles.Dev], scopedRoles: [ScopedAccountRoles.Tester]);

        Assert.False(ToolApplicationAccessPolicy.CanAccessFromHostContext(development, testerContext));
        Assert.True(ToolApplicationAccessPolicy.CanAccessFromHostContext(development, devContext));
        Assert.True(ToolApplicationAccessPolicy.CanAccessFromHostContext(testing, testerContext));
        Assert.True(ToolApplicationAccessPolicy.CanAccessFromHostContext(testing, devContext));
    }

    private static ToolRegistration Tool(ToolReleaseAudience audience) => new()
    {
        Slug = "release-test",
        Modes = [BuiltInSiteModes.DorksAndDice.Id],
        IntegrationType = ToolIntegrationType.EmbeddedModule,
        IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
        UpstreamBaseUrl = "http://release-test:8080",
        FrontendEntryPoint = "/app.js",
        ReleaseAudience = audience,
        AllowAnonymous = audience == ToolReleaseAudience.Public,
        Enabled = true
    };

    private static ToolHostAuthenticationContext HostContext(
        IReadOnlyList<string>? globalRoles = null,
        IReadOnlyList<string>? scopedRoles = null) => new()
    {
        ToolKey = "source",
        ToolSlug = "source",
        SiteMode = BuiltInSiteModes.DorksAndDice.Id,
        User = new ToolHostUserContext
        {
            Id = "11111111-2222-3333-4444-555555555555",
            DisplayName = "Release tester"
        },
        GlobalRoles = globalRoles ?? [],
        ScopedRoles = scopedRoles ?? []
    };

    private static ClaimsPrincipal ScopedPrincipal(string scope, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "scoped-user"),
            new Claim(AccountClaimTypes.ScopedRole, $"{scope}:{role}")
        ],
        authenticationType: "test"));

    private static ClaimsPrincipal GlobalPrincipal(string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "global-user"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "test"));

    private static ClaimsPrincipal AuthenticatedPrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "member-user")
        ],
        authenticationType: "test"));
}

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolReleaseAudienceIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public ToolReleaseAudienceIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestingToolHidesFromUnauthorizedUsersAndAllowsTesterOrInheritedDevAuthority()
    {
        var tool = await RegisterAsync(ToolReleaseAudience.Testing);
        try
        {
            using (var anonymousResponse = await SendAsync(tool))
            {
                Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);
                var html = await anonymousResponse.Content.ReadAsStringAsync();
                Assert.Contains("This page may not exist, or you may need to sign in", html);
                Assert.Contains("/account/login", html);
            }
            using (var signedInResponse = await SendAsync(tool, roles: "Member"))
            {
                Assert.Equal(HttpStatusCode.NotFound, signedInResponse.StatusCode);
                Assert.DoesNotContain("Sign in</a>", await signedInResponse.Content.ReadAsStringAsync());
            }
            Assert.Equal(
                HttpStatusCode.OK,
                (await SendAsync(
                    tool,
                    scopedRoles: $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Tester}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(tool, roles: AccountRoles.Dev)).StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task DevelopmentToolRequiresTrustedDevAuthority()
    {
        var tool = await RegisterAsync(ToolReleaseAudience.Development);
        try
        {
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendAsync(
                    tool,
                    scopedRoles: $"{BuiltInSiteModes.DorksAndDice.Id}:{ScopedAccountRoles.Tester}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(tool, roles: AccountRoles.Dev)).StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await SendAsync(tool, roles: AccountRoles.Dev, trustedPreview: true)).StatusCode);
        }
        finally
        {
            await DeleteAsync(tool.Id);
        }
    }

    private async Task<ToolRegistration> RegisterAsync(ToolReleaseAudience audience)
    {
        var now = DateTimeOffset.UtcNow;
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Key = $"release-{Guid.NewGuid():N}",
            Slug = $"release-{Guid.NewGuid():N}",
            DisplayName = "Release Audience Test",
            Modes = [BuiltInSiteModes.DorksAndDice.Id],
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            ReleaseAudience = audience,
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().SaveAsync(tool);
        return tool;
    }

    private async Task DeleteAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IToolRegistry>().DeleteAsync(id);
    }

    private async Task<HttpResponseMessage> SendAsync(
        ToolRegistration tool,
        string? roles = null,
        string? scopedRoles = null,
        bool trustedPreview = false)
    {
        var host = trustedPreview ? "localhost" : "dorks-and-dice.com";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"http://{host}/tools/{tool.Slug}");
        request.Headers.Host = host;
        if (roles is not null)
        {
            request.Headers.Add(TestRoleAuthenticationHandler.RolesHeader, roles);
        }
        if (scopedRoles is not null)
        {
            request.Headers.Add(TestRoleAuthenticationHandler.ScopedRolesHeader, scopedRoles);
        }
        if (trustedPreview)
        {
            request.Headers.Add(
                "Cookie",
                $"{SiteModeValues.DevelopmentSiteModeCookie}={BuiltInSiteModes.DorksAndDice.Id}");
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return await client.SendAsync(request);
    }
}
