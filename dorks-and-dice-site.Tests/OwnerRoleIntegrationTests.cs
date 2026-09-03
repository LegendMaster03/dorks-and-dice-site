using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class OwnerRoleIntegrationTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task OwnerInheritsTrustedAdminDeveloperAndEditorPrivileges()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"owner-inheritance-{Guid.NewGuid():N}@example.test";
        await CreateConfirmedUserWithRoleAsync(factory.Services, email, AccountRoles.Owner);

        using var client = CreateTrustedClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, email)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/development")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/editor/content")).StatusCode);
    }

    [Fact]
    public async Task OnlyOwnerCanAssignAdminAndDevAndOwnerItselfIsNotUiAssignable()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var ownerEmail = $"owner-delegation-{Guid.NewGuid():N}@example.test";
        var adminEmail = $"admin-delegation-{Guid.NewGuid():N}@example.test";
        var targetEmail = $"target-delegation-{Guid.NewGuid():N}@example.test";
        await CreateConfirmedUserWithRoleAsync(factory.Services, ownerEmail, AccountRoles.Owner);
        await CreateConfirmedUserWithRoleAsync(factory.Services, adminEmail, AccountRoles.Admin);
        var targetId = await CreateConfirmedUserWithRoleAsync(factory.Services, targetEmail, null);

        using (var ownerClient = CreateTrustedClient(factory))
        {
            Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(ownerClient, ownerEmail)).StatusCode);
            var token = await GetAccountManagementTokenAsync(ownerClient, targetId);

            using var assignDev = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = AccountRoles.Dev,
                ["enabled"] = "true",
                ["__RequestVerificationToken"] = token
            });
            var assignDevResponse = await ownerClient.PostAsync($"/admin/accounts/{targetId}/global-role", assignDev);
            Assert.Equal(HttpStatusCode.Redirect, assignDevResponse.StatusCode);

            token = await GetAccountManagementTokenAsync(ownerClient, targetId);
            using var assignOwner = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = AccountRoles.Owner,
                ["enabled"] = "true",
                ["__RequestVerificationToken"] = token
            });
            var assignOwnerResponse = await ownerClient.PostAsync($"/admin/accounts/{targetId}/global-role", assignOwner);
            Assert.Equal(HttpStatusCode.BadRequest, assignOwnerResponse.StatusCode);
        }

        using (var adminClient = CreateTrustedClient(factory))
        {
            Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(adminClient, adminEmail)).StatusCode);
            var token = await GetAccountManagementTokenAsync(adminClient, targetId);
            using var assignAdmin = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = AccountRoles.Admin,
                ["enabled"] = "true",
                ["__RequestVerificationToken"] = token
            });
            var assignAdminResponse = await adminClient.PostAsync($"/admin/accounts/{targetId}/global-role", assignAdmin);
            Assert.Equal(HttpStatusCode.Forbidden, assignAdminResponse.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByIdAsync(targetId.ToString());
        Assert.NotNull(target);
        Assert.True(await userManager.IsInRoleAsync(target, AccountRoles.Dev));
        Assert.False(await userManager.IsInRoleAsync(target, AccountRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(target, AccountRoles.Owner));
    }

    [Fact]
    public async Task OwnerCanNotDeleteOwnAccountThroughUi()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"owner-delete-{Guid.NewGuid():N}@example.test";
        var userId = await CreateConfirmedUserWithRoleAsync(factory.Services, email, AccountRoles.Owner);

        using var client = CreateTrustedClient(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, email)).StatusCode);
        var deletePage = await client.GetAsync("/account/delete");
        Assert.Equal(HttpStatusCode.OK, deletePage.StatusCode);
        var token = ExtractAntiforgeryToken(await deletePage.Content.ReadAsStringAsync());

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/account/delete", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Owner accounts can not be deleted through the UI", html, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(owner);
        Assert.Null(owner.DeletedAt);
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
            DisplayName = "Owner Role Test User",
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

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }
}
