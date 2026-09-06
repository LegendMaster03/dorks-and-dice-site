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

        if (modeContext.SyntheticMode is not null)
        {
            if (!modeContext.HasTrustedAccess)
            {
                return false;
            }

            // Synthetic Development has no generated "Editor @ development" role. Global Editor
            // authority applies across the control plane, while a scoped Editor may still use the
            // editor when Development is previewing that editor's normal mode. This does not grant
            // the scoped Editor authority over any other mode.
            if (string.Equals(role, ScopedAccountRoles.Editor, StringComparison.Ordinal)
                && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor))
            {
                return true;
            }

            return modeContext.ActiveModeId is { Length: > 0 } previewModeId
                && AccountRoleHierarchy.PrincipalHasScopedRole(principal, previewModeId, role);
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
