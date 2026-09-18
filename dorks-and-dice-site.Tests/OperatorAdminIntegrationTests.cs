using System.Net;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class OperatorAdminIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task AgentAdministrationIsVisibleAndAvailableOnlyToOwner()
    {
        using var owner = CreateTrustedClient(AccountRoles.Owner, AccountRoles.Admin);
        using var ownerAdmin = await owner.GetAsync("/admin");
        var ownerHtml = await ownerAdmin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, ownerAdmin.StatusCode);
        Assert.Contains("/admin/agents", ownerHtml, StringComparison.Ordinal);

        using var admin = CreateTrustedClient(AccountRoles.Admin);
        using var adminPage = await admin.GetAsync("/admin");
        var adminHtml = await adminPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
        Assert.DoesNotContain("/admin/agents", adminHtml, StringComparison.Ordinal);

        using var denied = await admin.GetAsync("/admin/agents");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCreateAgentIssueCredentialAndRevokeCredential()
    {
        using var client = CreateTrustedClient(AccountRoles.Owner, AccountRoles.Admin);
        var displayName = $"Agent Admin Test {Guid.NewGuid():N}";

        using var createPage = await client.GetAsync("/admin/agents/create");
        Assert.Equal(HttpStatusCode.OK, createPage.StatusCode);
        var createToken = ExtractAntiforgeryToken(await createPage.Content.ReadAsStringAsync());

        using var createForm = new FormUrlEncodedContent(
        [
            new("DisplayName", displayName),
            new("Roles", AccountRoles.GlobalEditor),
            new("Roles", AccountRoles.RulesLawyer),
            new("CredentialName", "initial"),
            new("__RequestVerificationToken", createToken)
        ]);
        using var created = await client.PostAsync("/admin/agents/create", createForm);
        var createdHtml = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        Assert.Contains("Agent created", createdHtml, StringComparison.Ordinal);
        Assert.Contains("ddop_v1_", createdHtml, StringComparison.Ordinal);

        Guid userId;
        Guid firstCredentialId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var agent = userManager.Users.Single(user => user.DisplayName == displayName);
            userId = agent.Id;
            Assert.Equal(AccountKind.ServicePrincipal, agent.AccountKind);
            Assert.Null(agent.PasswordHash);
            Assert.True(await userManager.IsInRoleAsync(agent, AccountRoles.GlobalEditor));
            Assert.True(await userManager.IsInRoleAsync(agent, AccountRoles.RulesLawyer));
            Assert.False(await userManager.IsInRoleAsync(agent, AccountRoles.Owner));

            var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();
            var stored = await credentials.GetForUserAsync(userId);
            firstCredentialId = Assert.Single(stored).Id;
        }

        using var details = await client.GetAsync($"/admin/agents/{userId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains(displayName, detailsHtml, StringComparison.Ordinal);
        Assert.Contains(firstCredentialId.ToString(), detailsHtml, StringComparison.Ordinal);
        var issueToken = ExtractAntiforgeryToken(detailsHtml);

        using var issueForm = new FormUrlEncodedContent(
        [
            new("CredentialName", "second"),
            new("__RequestVerificationToken", issueToken)
        ]);
        using var issued = await client.PostAsync($"/admin/agents/{userId}/credentials", issueForm);
        var issuedHtml = await issued.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.Equal("no-store", issued.Headers.CacheControl?.ToString());
        Assert.Contains("Credential created", issuedHtml, StringComparison.Ordinal);
        Assert.Contains("ddop_v1_", issuedHtml, StringComparison.Ordinal);

        using var refreshed = await client.GetAsync($"/admin/agents/{userId}");
        var refreshedHtml = await refreshed.Content.ReadAsStringAsync();
        var revokeToken = ExtractAntiforgeryToken(refreshedHtml);

        using var revokeForm = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", revokeToken)
        ]);
        using var revoked = await client.PostAsync(
            $"/admin/agents/{userId}/credentials/{firstCredentialId}/revoke",
            revokeForm);
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var credentialService = verifyScope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();
        var finalCredentials = await credentialService.GetForUserAsync(userId);
        Assert.Equal(2, finalCredentials.Count);
        Assert.NotNull(finalCredentials.Single(value => value.Id == firstCredentialId).RevokedAt);
        Assert.Null(finalCredentials.Single(value => value.Id != firstCredentialId).RevokedAt);
    }

    [Fact]
    public async Task AgentCreationRejectsOwnerRole()
    {
        using var client = CreateTrustedClient(AccountRoles.Owner, AccountRoles.Admin);
        using var createPage = await client.GetAsync("/admin/agents/create");
        var token = ExtractAntiforgeryToken(await createPage.Content.ReadAsStringAsync());

        using var form = new FormUrlEncodedContent(
        [
            new("DisplayName", $"Owner Agent {Guid.NewGuid():N}"),
            new("Roles", AccountRoles.Owner),
            new("CredentialName", "initial"),
            new("__RequestVerificationToken", token)
        ]);
        using var response = await client.PostAsync("/admin/agents/create", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not assignable", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ddop_v1_", html, StringComparison.Ordinal);
    }

    private HttpClient CreateTrustedClient(params string[] roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add(
            TestRoleAuthenticationHandler.RolesHeader,
            string.Join(',', roles));
        return client;
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
