using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class AccountAuthenticationTests
{
    [Fact]
    public async Task RegistrationRequiresEmailConfirmationBeforeAuthenticatedSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

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
        Assert.Equal(SiteMode.DorksAndDice, confirmationMessage.SiteMode);
        Assert.Equal(email, confirmationMessage.Recipient);
        Assert.Contains("Dorks & Dice", confirmationMessage.Subject, StringComparison.Ordinal);

        var confirmationUrl = confirmationMessage.TextBody
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        var confirmation = await client.GetAsync(confirmationUrl);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var confirmationHtml = await confirmation.Content.ReadAsStringAsync();
        Assert.Contains("Email confirmed", confirmationHtml, StringComparison.Ordinal);

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
        var accountHtml = await accountPage.Content.ReadAsStringAsync();
        Assert.Contains(displayName, accountHtml, StringComparison.Ordinal);

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
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new IdentityWebApplicationFactory(connectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);

        var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);

        var loginLocation = account.Headers.Location;
        Assert.NotNull(loginLocation);
        var loginPath = loginLocation.IsAbsoluteUri
            ? loginLocation.AbsolutePath
            : loginLocation.OriginalString.Split('?', 2)[0];
        Assert.Equal("/account/login", loginPath);
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
