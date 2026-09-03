using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;

namespace dorks_and_dice_site.Services.Identity;

public sealed record ModeScopedRoleRequirement(string Role) : IAuthorizationRequirement;

public sealed class ModeScopedRoleAuthorizationHandler : AuthorizationHandler<ModeScopedRoleRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ModeScopedRoleAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModeScopedRoleRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null || context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var siteMode = httpContext.GetSiteModeContext().SiteMode;
        if (!AccountRoleScopes.TryGetScope(siteMode, out var scope) || scope is null)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(requirement.Role, ScopedAccountRoles.Editor, StringComparison.Ordinal)
            && (context.User.IsInRole(AccountRoles.GlobalEditor)
                || context.User.IsInRole(AccountRoles.Admin)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var expectedValue = $"{scope}:{requirement.Role}";
        if (context.User.HasClaim(AccountClaimTypes.ScopedRole, expectedValue))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
