using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;

namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Removes Trusted Access-only global role claims from public requests while preserving any
/// non-privileged authority those roles inherit. The underlying Identity assignments remain
/// unchanged: Owner/Admin/Dev stay unavailable publicly, while safe inherited global and scoped
/// capabilities remain available.
/// </summary>
public sealed class TrustedPrivilegeClaimsTransformation : IClaimsTransformation
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SiteModeOptions _siteModeOptions;
    private readonly ISiteModeRegistry _siteModeRegistry;

    public TrustedPrivilegeClaimsTransformation(
        IHttpContextAccessor httpContextAccessor,
        SiteModeOptions siteModeOptions,
        ISiteModeRegistry siteModeRegistry)
    {
        _httpContextAccessor = httpContextAccessor;
        _siteModeOptions = siteModeOptions;
        _siteModeRegistry = siteModeRegistry;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null
            && TrustedAccessEvaluator.IsAuthorized(httpContext, _siteModeOptions))
        {
            return Task.FromResult(principal);
        }

        var transformed = new ClaimsPrincipal(
            principal.Identities.Select(identity => new ClaimsIdentity(identity)));

        foreach (var identity in transformed.Identities)
        {
            var privilegedRoleClaims = identity.Claims
                .Where(claim => string.Equals(claim.Type, identity.RoleClaimType, StringComparison.Ordinal)
                    && AccountRoles.TrustedPrivileged.Contains(claim.Value, StringComparer.Ordinal))
                .ToList();

            var safeInheritedRoles = privilegedRoleClaims
                .SelectMany(claim => AccountRoleHierarchy.GetInheritedGlobalRoles(claim.Value))
                .Where(role => !AccountRoles.TrustedPrivileged.Contains(role, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var role in safeInheritedRoles)
            {
                if (!identity.HasClaim(identity.RoleClaimType, role))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
                }
            }

            foreach (var claim in privilegedRoleClaims)
            {
                identity.RemoveClaim(claim);
            }
        }

        // A trusted global role can inherit a safe mode-scoped capability. Materialize any such
        // authority that was lost when trusted-only role claims were stripped. This keeps the
        // public principal semantically equivalent for safe scoped roles without exposing the
        // privileged global role itself. Resolve registered modes dynamically so future modes do
        // not require another hard-coded identity rule.
        var targetIdentity = transformed.Identities.FirstOrDefault(identity => identity.IsAuthenticated)
            ?? transformed.Identities.FirstOrDefault();
        if (targetIdentity is not null)
        {
            foreach (var mode in _siteModeRegistry.All)
            {
                foreach (var scopedRole in ScopedAccountRoles.ForScope(mode.Id))
                {
                    if (AccountRoleHierarchy.PrincipalHasScopedRole(principal, mode.Id, scopedRole)
                        && !AccountRoleHierarchy.PrincipalHasScopedRole(transformed, mode.Id, scopedRole))
                    {
                        targetIdentity.AddClaim(new Claim(
                            AccountClaimTypes.ScopedRole,
                            $"{mode.Id}:{scopedRole}"));
                    }
                }
            }
        }

        return Task.FromResult(transformed);
    }
}
