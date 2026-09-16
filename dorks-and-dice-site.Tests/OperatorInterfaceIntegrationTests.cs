using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content;
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

        using (var scope = factory.Services.CreateScope())
        {
            var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();
            Assert.True(await credentials.RevokeAsync(principal.CredentialId));
        }

        using (var revoked = await client.GetAsync("/operator/v1/me"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }
    }

    [Fact]
    public async Task GlobalEditorOperatorUsesExistingAuthoringRevisionAndAuditBoundaries()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var principal = await CreateServicePrincipalAsync(factory.Services, [AccountRoles.GlobalEditor]);
        string metadataJson;
        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            metadataJson = authoring.GetNew("External").Document.MetadataJson;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var request = new OperatorContentWriteRequest
        {
            Id = $"operator-content-{suffix}",
            Slug = $"operator-content-{suffix}",
            MetadataJson = metadataJson,
            Tags = ["article"],
            VisibleModes = [SiteModeValues.DorksAndDiceModeValue, SiteModeValues.ProfessionalModeValue],
            BodyFormat = "markdown",
            Body = "## Operator draft\n\nFirst revision."
        };

        using var client = CreateOperatorClient(factory, principal.Token);
        OperatorContentDocumentResponse created;
        using (var create = await client.PostAsJsonAsync("/operator/v1/content?source=External", request))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            created = (await create.Content.ReadFromJsonAsync<OperatorContentDocumentResponse>())!;
            Assert.Equal(request.Id, created.Id);
            Assert.Equal(request.VisibleModes.OrderBy(value => value), created.VisibleModes.OrderBy(value => value));
            Assert.Single(created.History);
        }

        var saveRequest = new OperatorContentWriteRequest
        {
            Id = created.Id,
            Slug = created.Slug,
            ExpectedRevisionId = created.RevisionId,
            MetadataJson = created.MetadataJson,
            Tags = created.Tags,
            VisibleModes = created.VisibleModes,
            BodyFormat = created.BodyFormat,
            Body = created.Body + "\n\nSecond revision."
        };

        OperatorContentDocumentResponse saved;
        using (var save = await client.PutAsJsonAsync(
                   $"/operator/v1/content/External/{created.Slug}",
                   saveRequest))
        {
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
            saved = (await save.Content.ReadFromJsonAsync<OperatorContentDocumentResponse>())!;
            Assert.NotEqual(created.RevisionId, saved.RevisionId);
            Assert.Equal(2, saved.History.Count);
            Assert.Contains("Second revision.", saved.Body, StringComparison.Ordinal);
        }

        using (var stale = await client.PutAsJsonAsync(
                   $"/operator/v1/content/External/{created.Slug}",
                   saveRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        using (var preview = await client.PostAsJsonAsync(
                   $"/operator/v1/content/External/{created.Slug}/preview",
                   new OperatorContentPreviewRequest
                   {
                       BodyFormat = "markdown",
                       Body = "## Preview heading"
                   }))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            var result = (await preview.Content.ReadFromJsonAsync<OperatorContentPreviewResponse>())!;
            Assert.Contains("Preview heading", result.Html, StringComparison.Ordinal);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var audits = await db.OperatorAuditRecords
                .Where(value => value.UserId == principal.UserId)
                .ToListAsync();
            Assert.Contains(audits, value => value.Capability == "content.create" && value.Outcome == "Succeeded");
            Assert.Contains(audits, value => value.Capability == "content.save_revision" && value.Outcome == "Succeeded");
            Assert.Contains(audits, value => value.Capability == "content.save_revision" && value.Outcome == "Failed");
            Assert.Contains(audits, value => value.Capability == "content.preview" && value.Outcome == "Succeeded");
        }
    }

    [Fact]
    public async Task ToolGatewayIssuesNormalToolHostContextForOperatorServicePrincipal()
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

        var principal = await CreateServicePrincipalAsync(factory.Services, [AccountRoles.RulesLawyer]);
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = $"operator-test-{Guid.NewGuid():N}",
            DisplayName = "Operator Test Tool",
            IntegrationType = ToolIntegrationType.EmbeddedModule,
            IntegrationContractVersion = ToolIntegrationContractVersions.EmbeddedModuleCurrent,
            OperatorContractVersion = ToolOperatorContractVersions.Current,
            OperatorManifestPath = "/operator/manifest",
            UpstreamBaseUrl = "http://operator-test-tool",
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
            using var client = CreateOperatorClient(factory, principal.Token);
            using (var capabilities = await client.GetAsync("/operator/v1/capabilities"))
            {
                Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
                var response = (await capabilities.Content.ReadFromJsonAsync<OperatorCapabilitiesResponse>())!;
                Assert.Contains(response.Tools, value => value.Slug == tool.Slug);
            }

            using (var manifest = await client.GetAsync($"/operator/v1/tools/{tool.Slug}/manifest"))
            {
                Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
            }

            Assert.Equal("/operator/manifest", proxy.Path);
            Assert.Equal(tool.Slug, proxy.ToolSlug);
            Assert.NotNull(proxy.AuthenticationContext);
            Assert.Equal(principal.UserId.ToString("D"), proxy.AuthenticationContext!.User.Id);
            Assert.Contains(AccountRoles.RulesLawyer, proxy.AuthenticationContext.GlobalRoles);
            Assert.Empty(proxy.AuthenticationContext.Campaigns);
        }
        finally
        {
            await registry.DeleteAsync(tool.Id);
        }
    }

    private static HttpClient CreateOperatorClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

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
                Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
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
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        if (roles.Count > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(user, roles);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        var credential = await credentials.CreateAsync(user.Id, "integration-test");
        return new CreatedOperatorPrincipal(user.Id, credential.Credential.Id, credential.Token);
    }

    private sealed record CreatedOperatorPrincipal(Guid UserId, Guid CredentialId, string Token);

    private sealed class RecordingToolProxyService : IToolProxyService
    {
        public string? Path { get; private set; }
        public string? ToolSlug { get; private set; }
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
            if (!ToolAuthenticationTickets.TryRedeem(tool.Slug, authenticationTicket, out var authenticationContext)
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
