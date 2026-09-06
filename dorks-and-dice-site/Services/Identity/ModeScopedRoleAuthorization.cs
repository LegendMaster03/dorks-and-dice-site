using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;

namespace dorks_and_dice_site.Services.Identity;

public sealed record ModeScopedRoleRequirement(string Role) : IAuthorizationRequirement;

public static class ModeScopedRoleAccess
{
    public static bool PrincipalHasRoleForContext(
        ClaimsPrincipal principal,
        SiteModeContext modeContext,
        string role)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(modeContext);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // Synthetic Development is a global control-plane mode rather than a normal tenant
        // scope. It receives no generated "Editor @ development" role. Editor authority in this
        // context therefore comes from Global Editor (and its normal Admin/Owner inheritance).
        if (modeContext.SyntheticMode is not null)
        {
            return modeContext.HasTrustedAccess
                && string.Equals(role, ScopedAccountRoles.Editor, StringComparison.Ordinal)
                && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor);
        }

        var scope = modeContext.ActiveModeId;

        // Temporary compatibility for tests/callers that still construct SiteModeContext using
        // only the legacy enum. Runtime middleware supplies ActiveMode for normal hosted modes.
        if (scope is null)
        {
            AccountRoleScopes.TryGetScope(modeContext.SiteMode, out scope);
        }

        return scope is not null
            && AccountRoleHierarchy.PrincipalHasScopedRole(principal, scope, role);
    }
}

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
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        if (ModeScopedRoleAccess.PrincipalHasRoleForContext(
                context.User,
                httpContext.GetSiteModeContext(),
                requirement.Role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
