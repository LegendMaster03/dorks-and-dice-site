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

        // Global role inheritance is resolved from the same hierarchy shown in account
        // management. Global Editor therefore remains valid on shared editor routes even
        // when Trusted Preview has no normal site mode selected.
        if (string.Equals(requirement.Role, ScopedAccountRoles.Editor, StringComparison.Ordinal)
            && AccountRoleHierarchy.PrincipalHasGlobalRole(context.User, AccountRoles.GlobalEditor))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var modeContext = httpContext.GetSiteModeContext();
        var scope = modeContext.ActiveModeId;

        // Temporary compatibility for tests/callers that still construct SiteModeContext
        // using only the legacy enum. Runtime middleware now supplies ActiveMode directly.
        if (scope is null)
        {
            AccountRoleScopes.TryGetScope(modeContext.SiteMode, out scope);
        }

        if (scope is not null
            && AccountRoleHierarchy.PrincipalHasScopedRole(context.User, scope, requirement.Role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
