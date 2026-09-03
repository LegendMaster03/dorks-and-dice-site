using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class ModeScopedRoleAuthorizationTests
{
    [Fact]
    public async Task DorksAndDiceEditorIsAuthorizedOnlyInDorksAndDiceMode()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Professional));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task ProfessionalEditorIsAuthorizedOnlyInProfessionalMode()
    {
        var user = CreateScopedEditor($"{AccountRoleScopes.Professional}:{ScopedAccountRoles.Editor}");

        Assert.True(await IsAuthorizedAsync(user, SiteMode.Professional));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task AdminIsGlobalEditorAcrossSiteModes()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Admin);

        Assert.True(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
        Assert.True(await IsAuthorizedAsync(user, SiteMode.Professional));
        Assert.True(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    [Fact]
    public async Task DevRoleDoesNotImplyEditorAccess()
    {
        var user = CreateGlobalRoleUser(AccountRoles.Dev);

        Assert.False(await IsAuthorizedAsync(user, SiteMode.DorksAndDice));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Professional));
        Assert.False(await IsAuthorizedAsync(user, SiteMode.Development));
    }

    private static ClaimsPrincipal CreateScopedEditor(string scopedRole)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(AccountClaimTypes.ScopedRole, scopedRole)
        ],
        authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateGlobalRoleUser(string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, SiteMode siteMode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            SiteMode = siteMode
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var handler = new ModeScopedRoleAuthorizationHandler(accessor);
        var requirement = new ModeScopedRoleRequirement(ScopedAccountRoles.Editor);
        var authorizationContext = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(authorizationContext);
        return authorizationContext.HasSucceeded;
    }
}
