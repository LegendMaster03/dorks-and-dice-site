using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class GlobalEditorRoleIntegrationTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task GlobalEditorInheritsBothScopedEditorRolesWithoutAdminOrDev()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"global-editor-{Guid.NewGuid():N}@example.test";
        await CreateConfirmedUserWithRoleAsync(factory.Services, email, AccountRoles.GlobalEditor);

        using var dorksClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(dorksClient, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await dorksClient.GetAsync("/editor/content")).StatusCode);
        AssertAccessDeniedRedirect(await dorksClient.GetAsync("/admin"));
        AssertAccessDeniedRedirect(await dorksClient.GetAsync("/development"));

        using var professionalClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://kylebarnett.com")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(professionalClient, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await professionalClient.GetAsync("/editor/content")).StatusCode);
    }

    [Fact]
    public async Task AdminCanAssignGlobalEditorButOnlyOwnerCanAssignAdminOrDev()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var adminEmail = $"global-editor-admin-{Guid.NewGuid():N}@example.test";
        var targetEmail = $"global-editor-target-{Guid.NewGuid():N}@example.test";
        await CreateConfirmedUserWithRoleAsync(factory.Services, adminEmail, AccountRoles.Admin);
        var targetId = await CreateConfirmedUserWithRoleAsync(factory.Services, targetEmail, null);

        using var client = CreateTrustedClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, adminEmail)).StatusCode);

        var token = await GetAccountManagementTokenAsync(client, targetId);
        using (var assignGlobalEditor = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["role"] = AccountRoles.GlobalEditor,
            ["enabled"] = "true",
            ["__RequestVerificationToken"] = token
        }))
        {
            Assert.Equal(
                HttpStatusCode.Redirect,
                (await client.PostAsync($"/admin/accounts/{targetId}/global-role", assignGlobalEditor)).StatusCode);
        }

        token = await GetAccountManagementTokenAsync(client, targetId);
        using (var assignDev = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["role"] = AccountRoles.Dev,
            ["enabled"] = "true",
            ["__RequestVerificationToken"] = token
        }))
        {
            Assert.Equal(
                HttpStatusCode.Redirect,
                (await client.PostAsync($"/admin/accounts/{targetId}/global-role", assignDev)).StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByIdAsync(targetId.ToString());
        Assert.NotNull(target);
        Assert.True(await userManager.IsInRoleAsync(target, AccountRoles.GlobalEditor));
        Assert.False(await userManager.IsInRoleAsync(target, AccountRoles.Dev));
        Assert.False(await userManager.IsInRoleAsync(target, AccountRoles.Admin));
    }

    [Fact]
    public async Task OwnerDetailsShowInheritanceAndAllowRemovingRedundantDirectRoles()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"owner-role-cleanup-{Guid.NewGuid():N}@example.test";
        var userId = await CreateConfirmedUserWithRoleAsync(factory.Services, email, AccountRoles.Owner);
        await AddRoleAsync(factory.Services, userId, AccountRoles.Admin);
        await AddRoleAsync(factory.Services, userId, AccountRoles.Dev);

        using var client = CreateTrustedClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, email)).StatusCode);

        var details = await client.GetAsync($"/admin/accounts/{userId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var html = await details.Content.ReadAsStringAsync();
        Assert.Contains("Inherited access", html, StringComparison.Ordinal);
        Assert.Contains("Assigned directly — also inherited through Owner", html, StringComparison.Ordinal);
        Assert.Contains("Global Editor", html, StringComparison.Ordinal);

        var token = ExtractAntiforgeryToken(html);
        using var removeAdmin = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["role"] = AccountRoles.Admin,
            ["enabled"] = "false",
            ["__RequestVerificationToken"] = token
        });
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await client.PostAsync($"/admin/accounts/{userId}/global-role", removeAdmin)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(owner);
        Assert.True(await userManager.IsInRoleAsync(owner, AccountRoles.Owner));
        Assert.False(await userManager.IsInRoleAsync(owner, AccountRoles.Admin));
        Assert.True(await userManager.IsInRoleAsync(owner, AccountRoles.Dev));
    }

    private static HttpClient CreateTrustedClient(IdentityWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<Guid> CreateConfirmedUserWithRoleAsync(
        IServiceProvider services,
        string email,
        string? role)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = "Global Editor Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(confirmationResult.Succeeded, string.Join(", ", confirmationResult.Errors.Select(error => error.Description)));

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                Assert.True(createRole.Succeeded, string.Join(", ", createRole.Errors.Select(error => error.Description)));
            }

            var addRole = await userManager.AddToRoleAsync(user, role);
            Assert.True(addRole.Succeeded, string.Join(", ", addRole.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private static async Task AddRoleAsync(IServiceProvider services, Guid userId, string role)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            Assert.True(createRole.Succeeded, string.Join(", ", createRole.Errors.Select(error => error.Description)));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var result = await userManager.AddToRoleAsync(user, role);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
    {
        var loginPage = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var token = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/account/login", form);
    }

    private static async Task<string> GetAccountManagementTokenAsync(HttpClient client, Guid userId)
    {
        var response = await client.GetAsync($"/admin/accounts/{userId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static void AssertAccessDeniedRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var path = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?', 2)[0];
        Assert.Equal("/account/access-denied", path);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }
}
