using System.Security.Claims;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;

namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Removes Trusted Access-only global role claims from public requests while preserving any
/// non-privileged authority those roles inherit. The underlying Identity assignments remain
/// unchanged: Owner/Admin/Dev stay unavailable publicly, but an Owner or Admin still retains
/// the Global Editor capability inherited through the role hierarchy.
/// </summary>
public sealed class TrustedPrivilegeClaimsTransformation : IClaimsTransformation
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SiteModeOptions _siteModeOptions;

    public TrustedPrivilegeClaimsTransformation(
        IHttpContextAccessor httpContextAccessor,
        SiteModeOptions siteModeOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _siteModeOptions = siteModeOptions;
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

        return Task.FromResult(transformed);
    }
}
