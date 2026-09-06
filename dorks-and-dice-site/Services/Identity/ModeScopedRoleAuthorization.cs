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

        var modeContext = httpContext.GetSiteModeContext();
        var scope = modeContext.ActiveModeId;

        // Trusted Preview has no concrete active mode at its root. Global Editor authority is
        // intentionally valid across every mode, so it can satisfy an Editor requirement there
        // without turning a mode-scoped Editor or Dev-only account into a global editor.
        if (modeContext.IsTrustedPreview
            && modeContext.HasTrustedAccess
            && string.Equals(requirement.Role, ScopedAccountRoles.Editor, StringComparison.Ordinal)
            && AccountRoleHierarchy.PrincipalHasGlobalRole(context.User, AccountRoles.GlobalEditor))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Temporary compatibility for tests/callers that still construct SiteModeContext
        // using only the legacy enum. Runtime middleware now supplies ActiveMode directly.
        if (scope is null)
        {
            AccountRoleScopes.TryGetScope(modeContext.SiteMode, out scope);
        }

        // All scoped-role inheritance is resolved through the hierarchy. This allows Owner ->
        // Admin -> Global Editor -> mode Editor inheritance without materializing redundant claims,
        // while still requiring a concrete active mode for normal editor routes.
        if (scope is not null
            && AccountRoleHierarchy.PrincipalHasScopedRole(context.User, scope, requirement.Role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
