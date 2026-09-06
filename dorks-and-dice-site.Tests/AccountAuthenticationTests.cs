using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AccountAuthenticationTests
{
    [Fact]
    public async Task RegistrationRequiresEmailConfirmationBeforeAuthenticatedSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

        var registerPage = await client.GetAsync("/account/register");
        Assert.Equal(HttpStatusCode.OK, registerPage.StatusCode);
        var registerHtml = await registerPage.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(registerHtml);
        var email = $"identity-test-{Guid.NewGuid():N}@example.test";
        const string displayName = "Identity Test User";
        const string password = "correct horse battery staple";

        using var registrationForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DisplayName"] = displayName,
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = antiforgeryToken
        });

        var registration = await client.PostAsync("/account/register", registrationForm);
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        Assert.Equal("/account/registration-pending", registration.Headers.Location?.OriginalString);
        var accountBeforeConfirmation = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, accountBeforeConfirmation.StatusCode);

        Assert.True(factory.EmailSender.Messages.TryDequeue(out var confirmationMessage));
        Assert.NotNull(confirmationMessage);
        Assert.Equal("Dorks & Dice", confirmationMessage.SenderIdentity.DisplayName);
        Assert.Equal("dorks-and-dice.com", confirmationMessage.SenderIdentity.Domain);
        Assert.Equal(email, confirmationMessage.Recipient);
        Assert.Contains("Dorks & Dice", confirmationMessage.Subject, StringComparison.Ordinal);

        var confirmationUrl = confirmationMessage.TextBody
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        var confirmation = await client.GetAsync(confirmationUrl);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Contains("Email confirmed", await confirmation.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var loginPage = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var loginToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = loginToken
        });

        var login = await client.PostAsync("/account/login", loginForm);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);

        var accountPage = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, accountPage.StatusCode);
        Assert.Contains(displayName, await accountPage.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(displayName, user.DisplayName);
        Assert.Equal(email, user.Email);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task AccountPageRequiresAuthenticationButPublicHomeDoesNot()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);
        var loginLocation = account.Headers.Location;
        Assert.NotNull(loginLocation);
        var loginPath = loginLocation.IsAbsoluteUri
            ? loginLocation.AbsolutePath
            : loginLocation.OriginalString.Split('?', 2)[0];
        Assert.Equal("/account/login", loginPath);
    }

    [Fact]
    public async Task PrivilegedRolesCanLoginPubliclyButPrivilegedFunctionsRequireTrustedAccess()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        await ResetPrivilegedRolesAsync(factory.Services);
        var email = $"admin-test-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateConfirmedUserAsync(factory.Services, email, password);
        await GrantPrivilegedRolesAsync(factory.Services, email);

        using var trustedClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(trustedClient, email, password)).StatusCode);
        var trustedAccount = await trustedClient.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, trustedAccount.StatusCode);
        var trustedAccountHtml = await trustedAccount.Content.ReadAsStringAsync();
        Assert.Contains("Admin", trustedAccountHtml, StringComparison.Ordinal);
        Assert.Contains("Dev", trustedAccountHtml, StringComparison.Ordinal);
        Assert.Contains("Trusted Access: available", trustedAccountHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Privileged account bootstrap", trustedAccountHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await trustedClient.GetAsync("/admin/accounts")).StatusCode);

        using var publicSessionRequest = new HttpRequestMessage(HttpMethod.Get, "/account");
        publicSessionRequest.Headers.Host = "dorks-and-dice.com";
        var publicSession = await trustedClient.SendAsync(publicSessionRequest);
        Assert.Equal(HttpStatusCode.OK, publicSession.StatusCode);
        Assert.Contains("Trusted Access: not available", await publicSession.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var publicAdminRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/accounts");
        publicAdminRequest.Headers.Host = "dorks-and-dice.com";
        var publicAdmin = await trustedClient.SendAsync(publicAdminRequest);
        Assert.Equal(HttpStatusCode.Redirect, publicAdmin.StatusCode);
        AssertAccessDeniedRedirect(publicAdmin);

        using var publicDevRequest = new HttpRequestMessage(HttpMethod.Get, "/development/content");
        publicDevRequest.Headers.Host = "dorks-and-dice.com";
        var publicDev = await trustedClient.SendAsync(publicDevRequest);
        Assert.Equal(HttpStatusCode.Redirect, publicDev.StatusCode);
        AssertAccessDeniedRedirect(publicDev);

        using var publicClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        var publicLogin = await LoginAsync(publicClient, email, password);
        Assert.Equal(HttpStatusCode.Redirect, publicLogin.StatusCode);
        Assert.Equal("/", publicLogin.Headers.Location?.OriginalString);
        var publicAccount = await publicClient.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, publicAccount.StatusCode);
        Assert.Contains("Trusted Access: not available", await publicAccount.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminRoleFormsSubmitBooleanValuesAndAssignmentsPersist()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        await ResetPrivilegedRolesAsync(factory.Services);
        var adminEmail = $"role-form-admin-{Guid.NewGuid():N}@example.test";
        var targetEmail = $"role-form-target-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateConfirmedUserAsync(factory.Services, adminEmail, password);
        await GrantPrivilegedRolesAsync(factory.Services, adminEmail);
        await GrantRoleAsync(factory.Services, adminEmail, AccountRoles.Owner);
        await CreateConfirmedUserAsync(factory.Services, targetEmail, password);

        Guid targetUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var target = await userManager.FindByEmailAsync(targetEmail);
            Assert.NotNull(target);
            targetUserId = target.Id;
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, adminEmail, password)).StatusCode);
        var details = await client.GetAsync($"/admin/accounts/{targetUserId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.DoesNotContain("name=\"enabled\" value=\"value\"", detailsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(new Regex("name=\\\"enabled\\\"\\s+value=\\\"true\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), detailsHtml);
        var token = ExtractAntiforgeryToken(detailsHtml);

        using (var globalRoleForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["role"] = AccountRoles.Dev,
            ["enabled"] = "true",
            ["__RequestVerificationToken"] = token
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/admin/accounts/{targetUserId}/global-role", globalRoleForm)).StatusCode);
        }

        using (var scopedRoleForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["scope"] = AccountRoleScopes.DorksAndDice,
            ["role"] = ScopedAccountRoles.Editor,
            ["enabled"] = "true",
            ["__RequestVerificationToken"] = token
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/admin/accounts/{targetUserId}/scoped-role", scopedRoleForm)).StatusCode);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var scopedRoleService = verificationScope.ServiceProvider.GetRequiredService<IScopedRoleService>();
        var targetUser = await verificationUserManager.FindByIdAsync(targetUserId.ToString());
        Assert.NotNull(targetUser);
        Assert.True(await verificationUserManager.IsInRoleAsync(targetUser, AccountRoles.Dev));
        Assert.True(await scopedRoleService.HasRoleAsync(targetUser, AccountRoleScopes.DorksAndDice, ScopedAccountRoles.Editor));
    }

    [Fact]
    public async Task LockedAdministratorDoesNotPermitRemovingTheOnlyUsableAdminRole()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        await ResetPrivilegedRolesAsync(factory.Services);
        var activeAdminEmail = $"active-admin-{Guid.NewGuid():N}@example.test";
        var lockedAdminEmail = $"locked-admin-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateConfirmedUserAsync(factory.Services, activeAdminEmail, password);
        await GrantPrivilegedRolesAsync(factory.Services, activeAdminEmail);
        await CreateConfirmedUserAsync(factory.Services, lockedAdminEmail, password);
        await GrantPrivilegedRolesAsync(factory.Services, lockedAdminEmail);
        await LockUserAsync(factory.Services, lockedAdminEmail);

        Guid activeAdminId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var activeAdmin = await userManager.FindByEmailAsync(activeAdminEmail);
            Assert.NotNull(activeAdmin);
            activeAdminId = activeAdmin.Id;
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, activeAdminEmail, password)).StatusCode);
        var details = await client.GetAsync($"/admin/accounts/{activeAdminId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var token = ExtractAntiforgeryToken(await details.Content.ReadAsStringAsync());
        using var removeAdminForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["role"] = AccountRoles.Admin,
            ["enabled"] = "false",
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/admin/accounts/{activeAdminId}/global-role", removeAdminForm);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var activeAdminUser = await verificationUserManager.FindByIdAsync(activeAdminId.ToString());
        Assert.NotNull(activeAdminUser);
        Assert.True(await verificationUserManager.IsInRoleAsync(activeAdminUser, AccountRoles.Admin));
    }

    [Fact]
    public async Task FinalUsableAdministratorCanNotDeleteOwnAccountWhenAnotherAdminIsLocked()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        await ResetPrivilegedRolesAsync(factory.Services);
        var email = $"last-admin-test-{Guid.NewGuid():N}@example.test";
        var lockedAdminEmail = $"locked-delete-admin-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateConfirmedUserAsync(factory.Services, email, password);
        await GrantPrivilegedRolesAsync(factory.Services, email);
        await CreateConfirmedUserAsync(factory.Services, lockedAdminEmail, password);
        await GrantPrivilegedRolesAsync(factory.Services, lockedAdminEmail);
        await LockUserAsync(factory.Services, lockedAdminEmail);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, email, password)).StatusCode);
        var deletePage = await client.GetAsync("/account/delete");
        Assert.Equal(HttpStatusCode.OK, deletePage.StatusCode);
        var token = ExtractAntiforgeryToken(await deletePage.Content.ReadAsStringAsync());
        using var deleteForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        var deletion = await client.PostAsync("/account/delete", deleteForm);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        Assert.Contains("The final active administrator account can not be deleted.", await deletion.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Null(user.DeletedAt);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetAsync("/account/login");
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

    private static async Task CreateConfirmedUserAsync(IServiceProvider services, string email, string password)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = "Administrator Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(confirmationResult.Succeeded, string.Join(", ", confirmationResult.Errors.Select(error => error.Description)));
    }

    private static async Task GrantPrivilegedRolesAsync(IServiceProvider services, string email)
    {
        foreach (var roleName in AccountRoles.Privileged)
        {
            await GrantRoleAsync(services, email, roleName);
        }
    }

    private static async Task GrantRoleAsync(IServiceProvider services, string email, string roleName)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            Assert.True(createRole.Succeeded, string.Join(", ", createRole.Errors.Select(error => error.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var addRole = await userManager.AddToRoleAsync(user, roleName);
            Assert.True(addRole.Succeeded, string.Join(", ", addRole.Errors.Select(error => error.Description)));
        }
    }

    private static async Task LockUserAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var enableResult = await userManager.SetLockoutEnabledAsync(user, true);
        Assert.True(enableResult.Succeeded, string.Join(", ", enableResult.Errors.Select(error => error.Description)));
        var lockResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        Assert.True(lockResult.Succeeded, string.Join(", ", lockResult.Errors.Select(error => error.Description)));
    }

    private static async Task ResetPrivilegedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in AccountRoles.TrustedPrivileged)
        {
            if (!await roleManager.RoleExistsAsync(roleName)) continue;
            foreach (var user in await userManager.GetUsersInRoleAsync(roleName))
            {
                var removeResult = await userManager.RemoveFromRoleAsync(user, roleName);
                Assert.True(removeResult.Succeeded, string.Join(", ", removeResult.Errors.Select(error => error.Description)));
            }
            var role = await roleManager.FindByNameAsync(roleName);
            Assert.NotNull(role);
            var deleteResult = await roleManager.DeleteAsync(role);
            Assert.True(deleteResult.Succeeded, string.Join(", ", deleteResult.Errors.Select(error => error.Description)));
        }
    }

    private static void AssertAccessDeniedRedirect(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?', 2)[0];
        Assert.Equal("/account/access-denied", path);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"([^\"]+)\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The form did not contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
