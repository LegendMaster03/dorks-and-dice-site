using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class ModeEditorInheritanceIntegrationTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task OwnerInheritsModeEditorAccessWithoutDirectScopedRoles()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new IdentityWebApplicationFactory(connectionString);
        var email = $"owner-mode-editor-{Guid.NewGuid():N}@example.test";
        await CreateConfirmedOwnerAsync(factory.Services, email);

        using var dorksClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(dorksClient, email)).StatusCode);
        var dorksLanding = await dorksClient.GetAsync("/editor");
        Assert.Equal(HttpStatusCode.OK, dorksLanding.StatusCode);
        var dorksHtml = await dorksLanding.Content.ReadAsStringAsync();
        Assert.Contains("Dorks &amp; Dice Editor", dorksHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Professional Editor", dorksHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await dorksClient.GetAsync("/editor/content")).StatusCode);

        using var professionalClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://kylebarnett.com")
        });
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(professionalClient, email)).StatusCode);
        var professionalLanding = await professionalClient.GetAsync("/editor");
        Assert.Equal(HttpStatusCode.OK, professionalLanding.StatusCode);
        var professionalHtml = await professionalLanding.Content.ReadAsStringAsync();
        Assert.Contains("Professional Editor", professionalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Dorks &amp; Dice Editor", professionalHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await professionalClient.GetAsync("/editor/content")).StatusCode);
    }

    private static async Task CreateConfirmedOwnerAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = "Owner Editor Test User",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);
        Assert.True(confirmationResult.Succeeded, string.Join(", ", confirmationResult.Errors.Select(error => error.Description)));

        if (!await roleManager.RoleExistsAsync(AccountRoles.Owner))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(AccountRoles.Owner));
            Assert.True(createRole.Succeeded, string.Join(", ", createRole.Errors.Select(error => error.Description)));
        }

        var addRole = await userManager.AddToRoleAsync(user, AccountRoles.Owner);
        Assert.True(addRole.Succeeded, string.Join(", ", addRole.Errors.Select(error => error.Description)));
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

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }
}
