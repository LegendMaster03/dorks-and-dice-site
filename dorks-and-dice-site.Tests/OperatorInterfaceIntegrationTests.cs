using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class OperatorInterfaceIntegrationTests
{
    [Fact]
    public async Task ServicePrincipalCredentialAuthenticatesCanBeRevokedAndCanNotUsePasswordSignIn()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        const string password = "operator password should never become an interactive path";
        var principal = await CreateServicePrincipalAsync(
            factory.Services,
            [AccountRoles.RulesLawyer],
            password);

        using (var scope = factory.Services.CreateScope())
        {
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(principal.UserId.ToString("D"));
            Assert.NotNull(user);
            var signIn = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
            Assert.True(signIn.IsNotAllowed);
        }

        using var client = CreateOperatorClient(factory, principal.Token);
        using (var response = await client.GetAsync("/operator/v1/me"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = (await response.Content.ReadFromJsonAsync<OperatorMeResponse>())!;
            Assert.Equal(principal.UserId, me.UserId);
            Assert.Equal(AccountKind.ServicePrincipal.ToString(), me.AccountKind);
            Assert.Contains(AccountRoles.RulesLawyer, me.GlobalRoles);
            Assert.True(response.Headers.Contains("X-Dorks-Operator-Invocation-Id"));
        }

        await RevokeCredentialAsync(factory.Services, principal.CredentialId);

        using var revoked = await client.GetAsync("/operator/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task ExpiredOperatorCredentialCanNotAuthenticate()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);
        await ExpireCredentialAsync(factory.Services, principal.CredentialId);

        using var client = CreateOperatorClient(factory, principal.Token);
        using var response = await client.GetAsync("/operator/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BrowserBootstrapCreatesNormalCookieAndUsesExistingToolHostPath()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var baseFactory = new IdentityWebApplicationFactory(connectionString);
        var proxy = new RecordingToolProxyService();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolProxyService>();
                services.AddSingleton<IToolProxyService>(proxy);
            });
        });

        var principal = await CreateServicePrincipalAsync(
            factory.Services,
            [AccountRoles.GlobalEditor, AccountRoles.RulesLawyer]);
        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);

        using var browser = CreateBrowserClient(factory);
        using (var consume = await browser.GetAsync(bootstrap.BootstrapUrl))
        {
            Assert.Equal(HttpStatusCode.Redirect, consume.StatusCode);
            Assert.Equal("/", consume.Headers.Location?.OriginalString);
            Assert.Equal("no-referrer", consume.Headers.GetValues("Referrer-Policy").Single());
            Assert.True(consume.Headers.Contains("X-Dorks-Operator-Invocation-Id"));
        }

        using (var account = await browser.GetAsync("/account"))
        {
            Assert.Equal(HttpStatusCode.OK, account.StatusCode);
            Assert.Contains(
                "Operator Integration Test",
                await account.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        using (var editor = await browser.GetAsync("/editor/content"))
        {
            Assert.Equal(HttpStatusCode.OK, editor.StatusCode);
        }

        using (var secondBrowser = CreateBrowserClient(factory))
        using (var secondConsume = await secondBrowser.GetAsync(bootstrap.BootstrapUrl))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, secondConsume.StatusCode);
        }

        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"bootstrap-test-{Guid.NewGuid():N}",
            DisplayName = "Bootstrap Test Tool",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            UpstreamBaseUrl = "http://bootstrap-test-tool",
            FrontendEntryPoint = "/app.js",
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var registry = factory.Services.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        try
        {
            using var upstream = await browser.GetAsync($"/tool-host/{tool.Slug}/api/upstream/ping");
            Assert.Equal(HttpStatusCode.OK, upstream.StatusCode);
            Assert.Equal("/ping", proxy.Path);
            Assert.Equal(tool.Slug, proxy.ToolSlug);
            Assert.NotNull(proxy.AuthenticationContext);
            Assert.Equal(principal.UserId.ToString("D"), proxy.AuthenticationContext!.User.Id);
            Assert.Contains(AccountRoles.GlobalEditor, proxy.AuthenticationContext.GlobalRoles);
            Assert.Contains(AccountRoles.RulesLawyer, proxy.AuthenticationContext.GlobalRoles);
            Assert.Equal(
                SiteModeValues.DorksAndDiceModeValue,
                proxy.AuthenticationContext.SiteMode);
            Assert.Empty(proxy.AuthenticationContext.Campaigns);
            Assert.True(string.IsNullOrWhiteSpace(proxy.BrowserAuthorizationHeader));
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.OperatorBrowserBootstraps
            .SingleAsync(value => value.Id == bootstrap.BootstrapId);
        Assert.Equal(principal.UserId, stored.UserId);
        Assert.Equal(principal.CredentialId, stored.CredentialId);
        Assert.NotEqual(Guid.Empty, stored.IssuanceInvocationId);
        Assert.NotNull(stored.ConsumedAt);

        var audits = await db.OperatorAuditRecords
            .Where(value => value.UserId == principal.UserId)
            .ToListAsync();
        Assert.Contains(audits, value =>
            value.Capability == "operator.browser_bootstrap"
            && value.Outcome == "Succeeded");
        Assert.Contains(audits, value =>
            value.Capability == "operator.browser_bootstrap.consume"
            && value.Outcome == "Succeeded");
        Assert.Contains(audits, value =>
            value.Capability == "operator.browser_bootstrap.consume"
            && value.Outcome == "AlreadyConsumed");

        Assert.Null(typeof(ToolRegistration).GetProperty("OperatorContractVersion"));
        Assert.Null(typeof(ToolRegistration).GetProperty("OperatorManifestPath"));
    }

    [Fact]
    public async Task OperatorCredentialDoesNotGrantUnassignedRulesLawyerThroughToolHost()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);

        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);
        using var browser = CreateBrowserClient(factory);
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await browser.GetAsync(bootstrap.BootstrapUrl)).StatusCode);

        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"rules-lawyer-negative-{Guid.NewGuid():N}",
            DisplayName = "Rules Lawyer Negative Test",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            Modes = [SiteModeValues.DorksAndDiceModeValue],
            AllowAnonymous = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var registry = factory.Services.GetRequiredService<IToolRegistry>();
        await registry.SaveAsync(tool);
        try
        {
            using var response = await browser.GetAsync($"/tool-host/{tool.Slug}/api/session");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var session = await response.Content.ReadFromJsonAsync<ToolHostApiSession>();
            Assert.NotNull(session);
            Assert.Equal(principal.UserId.ToString("D"), session.User.Id);
            Assert.Equal(SiteModeValues.DorksAndDiceModeValue, session.SiteMode);
            Assert.DoesNotContain(AccountRoles.RulesLawyer, session.GlobalRoles);
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    [Fact]
    public async Task RevokingCredentialInvalidatesExistingBootstrappedSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);

        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);
        using var browser = CreateBrowserClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(bootstrap.BootstrapUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/account")).StatusCode);

        await RevokeCredentialAsync(factory.Services, principal.CredentialId);

        using var rejected = await browser.GetAsync("/account");
        AssertLoginRedirect(rejected);
    }

    [Fact]
    public async Task ExpiringCredentialInvalidatesExistingBootstrappedSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);

        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);
        using var browser = CreateBrowserClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync(bootstrap.BootstrapUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/account")).StatusCode);

        await ExpireCredentialAsync(factory.Services, principal.CredentialId);

        using var rejected = await browser.GetAsync("/account");
        AssertLoginRedirect(rejected);
    }

    [Fact]
    public async Task CredentialBoundSessionsRemainIndependentForSameServicePrincipal()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var first = await CreateServicePrincipalAsync(factory.Services, []);
        var second = await CreateCredentialAsync(factory.Services, first.UserId, "second-session");

        using var firstOperatorClient = CreateOperatorClient(factory, first.Token);
        using var secondOperatorClient = CreateOperatorClient(factory, second.Token);
        var firstBootstrap = await IssueBootstrapAsync(firstOperatorClient);
        var secondBootstrap = await IssueBootstrapAsync(secondOperatorClient);

        using var firstBrowser = CreateBrowserClient(factory);
        using var secondBrowser = CreateBrowserClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await firstBrowser.GetAsync(firstBootstrap.BootstrapUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await secondBrowser.GetAsync(secondBootstrap.BootstrapUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await firstBrowser.GetAsync("/account")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await secondBrowser.GetAsync("/account")).StatusCode);

        await RevokeCredentialAsync(factory.Services, first.CredentialId);

        using var firstRejected = await firstBrowser.GetAsync("/account");
        AssertLoginRedirect(firstRejected);
        Assert.Equal(HttpStatusCode.OK, (await secondBrowser.GetAsync("/account")).StatusCode);
    }

    [Fact]
    public async Task RevokingCredentialBeforeBootstrapConsumptionPreventsSessionCreation()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);

        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);
        await RevokeCredentialAsync(factory.Services, principal.CredentialId);

        using var browser = CreateBrowserClient(factory);
        using var consume = await browser.GetAsync(bootstrap.BootstrapUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, consume.StatusCode);

        using var account = await browser.GetAsync("/account");
        AssertLoginRedirect(account);
    }

    [Fact]
    public async Task BrowserBootstrapExpiresBeforeItCanCreateCookieSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, []);

        using var operatorClient = CreateOperatorClient(factory, principal.Token);
        var bootstrap = await IssueBootstrapAsync(operatorClient);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var stored = await db.OperatorBrowserBootstraps
                .SingleAsync(value => value.Id == bootstrap.BootstrapId);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        using var browser = CreateBrowserClient(factory);
        using var consume = await browser.GetAsync(bootstrap.BootstrapUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, consume.StatusCode);

        using var account = await browser.GetAsync("/account");
        AssertLoginRedirect(account);

        using var auditScope = factory.Services.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audits = await auditDb.OperatorAuditRecords
            .Where(value => value.UserId == principal.UserId)
            .ToListAsync();
        Assert.Contains(audits, value =>
            value.Capability == "operator.browser_bootstrap.consume"
            && value.Outcome == "Expired");
    }

    [Fact]
    public async Task HumanApplicationCookieContinuesToAuthenticateNormally()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"human-cookie-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateHumanUserAsync(factory.Services, email, password);

        using var browser = CreateBrowserClient(factory);
        using var login = await LoginHumanAsync(browser, email, password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);

        using var account = await browser.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains("Human Cookie Test", await account.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task<OperatorBrowserBootstrapResponse> IssueBootstrapAsync(HttpClient operatorClient)
    {
        using var issue = await operatorClient.PostAsync("/operator/v1/browser-bootstrap", null);
        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);
        Assert.Equal("no-store", issue.Headers.CacheControl?.ToString());

        var bootstrap = await issue.Content.ReadFromJsonAsync<OperatorBrowserBootstrapResponse>();
        Assert.NotNull(bootstrap);
        Assert.NotEqual(Guid.Empty, bootstrap.BootstrapId);
        Assert.StartsWith("/operator/bootstrap?token=", bootstrap.BootstrapUrl, StringComparison.Ordinal);
        Assert.True(bootstrap.ExpiresAt > DateTimeOffset.UtcNow);
        return bootstrap;
    }

    private static HttpClient CreateOperatorClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpClient CreateBrowserClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

    private static async Task<CreatedOperatorPrincipal> CreateServicePrincipalAsync(
        IServiceProvider services,
        IReadOnlyList<string> roles,
        string? password = null)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                Assert.True(
                    roleResult.Succeeded,
                    string.Join(", ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        var userId = Guid.NewGuid();
        var email = $"operator-test-{userId:N}@service.invalid";
        var user = new ApplicationUser
        {
            Id = userId,
            AccountKind = AccountKind.ServicePrincipal,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Operator Integration Test",
            CreatedAt = DateTimeOffset.UtcNow,
            LockoutEnabled = false
        };
        var createResult = password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);
        Assert.True(
            createResult.Succeeded,
            string.Join(", ", createResult.Errors.Select(error => error.Description)));

        if (roles.Count > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(user, roles);
            Assert.True(
                roleResult.Succeeded,
                string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        var credential = await credentials.CreateAsync(user.Id, "integration-test");
        return new CreatedOperatorPrincipal(
            user.Id,
            credential.Credential.Id,
            credential.Token);
    }

    private static async Task<CreatedCredential> CreateCredentialAsync(
        IServiceProvider services,
        Guid userId,
        string name)
    {
        using var scope = services.CreateScope();
        var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();
        var created = await credentials.CreateAsync(userId, name);
        return new CreatedCredential(created.Credential.Id, created.Token);
    }

    private static async Task RevokeCredentialAsync(
        IServiceProvider services,
        Guid credentialId)
    {
        using var scope = services.CreateScope();
        var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();
        Assert.True(await credentials.RevokeAsync(credentialId));
    }

    private static async Task ExpireCredentialAsync(
        IServiceProvider services,
        Guid credentialId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var credential = await db.OperatorCredentials
            .SingleAsync(value => value.Id == credentialId);
        credential.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    private static async Task CreateHumanUserAsync(
        IServiceProvider services,
        string email,
        string password)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            AccountKind = AccountKind.Human,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Human Cookie Test",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(
            result.Succeeded,
            string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private static async Task<HttpResponseMessage> LoginHumanAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var loginPage = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var token = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/account/login", loginForm);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"([^\"]+)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The form did not contain an antiforgery token.");
        return match.Groups[1].Value;
    }

    private static void AssertLoginRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var path = response.Headers.Location!.IsAbsoluteUri
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location.OriginalString.Split('?', 2)[0];
        Assert.Equal("/account/login", path);
    }

    private sealed record CreatedOperatorPrincipal(
        Guid UserId,
        Guid CredentialId,
        string Token);

    private sealed record CreatedCredential(
        Guid CredentialId,
        string Token);

    private sealed class RecordingToolProxyService : IToolProxyService
    {
        public string? Path { get; private set; }
        public string? ToolSlug { get; private set; }
        public string? BrowserAuthorizationHeader { get; private set; }
        public ToolHostAuthenticationContext? AuthenticationContext { get; private set; }

        public Task ProxyAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }

        public async Task ProxyAuthenticatedAsync(
            HttpContext context,
            ToolRegistration tool,
            string path,
            string authenticationTicket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            Path = path;
            ToolSlug = tool.Slug;
            BrowserAuthorizationHeader = context.Request.Headers.Authorization.ToString();
            if (!ToolAuthenticationTickets.TryRedeem(
                    tool.Slug,
                    authenticationTicket,
                    out var authenticationContext)
                || authenticationContext is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            AuthenticationContext = authenticationContext;
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { ok = true }, cancellationToken);
        }
    }
}
