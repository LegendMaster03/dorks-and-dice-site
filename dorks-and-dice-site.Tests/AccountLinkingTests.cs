using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AccountLinkingTests
{
    [Fact]
    public void CatalogSupportsOAuthAndOpenIddictProviders()
    {
        var oauth = new OAuthAccountLinkProvider(new(
            "oauth-test",
            "OAuth Test",
            "OAuthTest",
            AccountLinkProtocol.OAuth));
        var openIddict = new OpenIddictAccountLinkProvider(new(
            "openiddict-test",
            "OpenIddict Test",
            "OpenIddictTest",
            AccountLinkProtocol.OpenIddict));

        var catalog = new AccountLinkProviderCatalog([oauth, openIddict]);

        Assert.Equal(2, catalog.All.Count);
        Assert.True(catalog.TryGet("oauth-test", out var resolvedOAuth));
        Assert.Equal(AccountLinkProtocol.OAuth, resolvedOAuth.Descriptor.Protocol);
        Assert.True(catalog.TryGet("OPENIDDICT-TEST", out var resolvedOpenIddict));
        Assert.Equal(AccountLinkProtocol.OpenIddict, resolvedOpenIddict.Descriptor.Protocol);
    }

    [Fact]
    public async Task AuthenticatedUserCanLinkAndDisconnectProvider()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var provider = new FakeAccountLinkProvider();
        using var factory = new IdentityWebApplicationFactory(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IAccountLinkProvider>(provider);
                });
            });

        var email = $"account-link-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = "Account Link Test",
                CreatedAt = DateTimeOffset.UtcNow
            };
            Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await userManager.ConfirmEmailAsync(user, token)).Succeeded);
            userId = user.Id;
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

        await LoginAsync(client, email, password);

        var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        var accountHtml = await account.Content.ReadAsStringAsync();
        Assert.Contains("Test Provider", accountHtml, StringComparison.Ordinal);
        Assert.Contains("Connect", accountHtml, StringComparison.Ordinal);

        var tokenBeforeConnect = ExtractAntiforgeryToken(accountHtml);
        using var connectForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenBeforeConnect
        });
        var connect = await client.PostAsync("/account/links/test-provider/connect", connectForm);
        Assert.Equal(HttpStatusCode.Redirect, connect.StatusCode);

        var nonce = provider.LastChallengeProperties?.Items[AccountLinkAuthenticationProperties.Nonce];
        Assert.False(string.IsNullOrWhiteSpace(nonce));

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            var storedNonce = await userManager.GetAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                AccountLinkTokenNames.Nonce(provider.Descriptor.Id));
            Assert.Equal(nonce, storedNonce);
        }

        provider.CallbackResult = new AccountLinkAuthenticationResult(
            new AccountLinkIdentity("external-user-123", "External Test User"),
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [AccountLinkAuthenticationProperties.ProviderId] = provider.Descriptor.Id,
                [AccountLinkAuthenticationProperties.UserId] = userId.ToString("D"),
                [AccountLinkAuthenticationProperties.Nonce] = nonce
            }));

        var callback = await client.GetAsync("/account/links/callback/test-provider");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/account", callback.Headers.Location?.OriginalString);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            var logins = await userManager.GetLoginsAsync(user);
            var login = Assert.Single(logins);
            Assert.Equal("test-provider", login.LoginProvider);
            Assert.Equal("external-user-123", login.ProviderKey);
            Assert.Null(await userManager.GetAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                AccountLinkTokenNames.Nonce(provider.Descriptor.Id)));
        }

        var linkedAccount = await client.GetAsync("/account");
        var linkedHtml = await linkedAccount.Content.ReadAsStringAsync();
        Assert.Contains("External Test User", linkedHtml, StringComparison.Ordinal);
        Assert.Contains("Disconnect", linkedHtml, StringComparison.Ordinal);

        var disconnectToken = ExtractAntiforgeryToken(linkedHtml);
        using var disconnectForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = disconnectToken
        });
        var disconnect = await client.PostAsync(
            "/account/links/test-provider/disconnect",
            disconnectForm);
        Assert.Equal(HttpStatusCode.Redirect, disconnect.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager =
            verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var verificationUser = await verificationUserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(verificationUser);
        Assert.Empty(await verificationUserManager.GetLoginsAsync(verificationUser));
    }

    [Fact]
    public async Task CallbackRejectsNonceThatDoesNotMatchPendingLink()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var provider = new FakeAccountLinkProvider();
        using var factory = new IdentityWebApplicationFactory(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IAccountLinkProvider>(provider);
                });
            });

        var email = $"account-link-nonce-{Guid.NewGuid():N}@example.test";
        const string password = "correct horse battery staple";
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = "Account Link Nonce Test",
                CreatedAt = DateTimeOffset.UtcNow
            };
            Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await userManager.ConfirmEmailAsync(user, token)).Succeeded);
            userId = user.Id;
            Assert.True((await userManager.SetAuthenticationTokenAsync(
                user,
                AccountLinkTokenNames.LoginProvider,
                AccountLinkTokenNames.Nonce(provider.Descriptor.Id),
                "expected-nonce")).Succeeded);
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        await LoginAsync(client, email, password);

        provider.CallbackResult = new AccountLinkAuthenticationResult(
            new AccountLinkIdentity("external-user-123", "External Test User"),
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [AccountLinkAuthenticationProperties.ProviderId] = provider.Descriptor.Id,
                [AccountLinkAuthenticationProperties.UserId] = userId.ToString("D"),
                [AccountLinkAuthenticationProperties.Nonce] = "replayed-or-wrong-nonce"
            }));

        var callback = await client.GetAsync("/account/links/callback/test-provider");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager =
            verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var verificationUser = await verificationUserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(verificationUser);
        Assert.Empty(await verificationUserManager.GetLoginsAsync(verificationUser));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
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
        var login = await client.PostAsync("/account/login", loginForm);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
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

    private sealed class FakeAccountLinkProvider : IAccountLinkProvider
    {
        public AccountLinkProviderDescriptor Descriptor { get; } = new(
            "test-provider",
            "Test Provider",
            IdentityConstants.ApplicationScheme,
            AccountLinkProtocol.OAuth);

        public AuthenticationProperties? LastChallengeProperties { get; private set; }
        public AccountLinkAuthenticationResult? CallbackResult { get; set; }

        public AuthenticationProperties CreateChallengeProperties(
            Guid userId,
            string nonce,
            string callbackPath)
        {
            LastChallengeProperties = new AuthenticationProperties
            {
                RedirectUri = callbackPath
            };
            LastChallengeProperties.Items[AccountLinkAuthenticationProperties.UserId] =
                userId.ToString("D");
            LastChallengeProperties.Items[AccountLinkAuthenticationProperties.Nonce] = nonce;
            return LastChallengeProperties;
        }

        public Task<AccountLinkAuthenticationResult?> AuthenticateAsync(HttpContext context) =>
            Task.FromResult(CallbackResult);

        public Task CleanupAsync(HttpContext context) => Task.CompletedTask;
    }
}
