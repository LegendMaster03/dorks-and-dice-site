using System.Security.Claims;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;

namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Removes privileged global role claims from the request principal when the
/// request does not pass Trusted Access. The underlying Identity role assignments
/// remain unchanged, so the account can still sign in and use ordinary account
/// features publicly while Admin and Dev authority stays unavailable.
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
                    && AccountRoles.Privileged.Contains(claim.Value, StringComparer.Ordinal))
                .ToList();

            foreach (var claim in privilegedRoleClaims)
            {
                identity.RemoveClaim(claim);
            }
        }

        return Task.FromResult(transformed);
    }
}
