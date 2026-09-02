using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class RemovedPrivilegeBootstrapIntegrationTests
{
    [Fact]
    public async Task RemovedBootstrapEndpointCanNotGrantPrivilegedRoles()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"removed-bootstrap-test-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        await CreateConfirmedUserAsync(factory.Services, email, password);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var loginPage = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var loginToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        using (var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = loginToken
        }))
        {
            var login = await client.PostAsync("/account/login", loginForm);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        }

        var accountPage = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, accountPage.StatusCode);
        var accountHtml = await accountPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Privileged account bootstrap", accountHtml, StringComparison.Ordinal);
        var antiforgeryToken = ExtractAntiforgeryToken(accountHtml);

        using var bootstrapForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        });
        var legacyBootstrapAttempt = await client.PostAsync("/account/bootstrap-privileged", bootstrapForm);
        Assert.Equal(HttpStatusCode.NotFound, legacyBootstrapAttempt.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(await userManager.IsInRoleAsync(user, AccountRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(user, AccountRoles.Dev));
    }

    private static async Task CreateConfirmedUserAsync(
        IServiceProvider services,
        string email,
        string password)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = "Removed Bootstrap Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(
            confirmationResult.Succeeded,
            string.Join(", ", confirmationResult.Errors.Select(error => error.Description)));
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]+name=\"__RequestVerificationToken\"[^>]+value=\"([^\"]+)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.True(match.Success, "The form did not contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
