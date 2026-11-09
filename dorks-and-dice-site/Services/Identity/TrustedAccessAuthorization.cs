using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;

namespace dorks_and_dice_site.Services.Identity;

public sealed class TrustedAccessRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Bridges the network/trusted-ingress evaluator into ASP.NET authorization.
/// Trusted access is a prerequisite for privileged controls, not itself
/// proof that the principal holds a privileged role.
/// </summary>
public sealed class TrustedAccessAuthorizationHandler : AuthorizationHandler<TrustedAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SiteModeOptions _siteModeOptions;

    public TrustedAccessAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        SiteModeOptions siteModeOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _siteModeOptions = siteModeOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TrustedAccessRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null
            && TrustedAccessEvaluator.IsAuthorized(httpContext, _siteModeOptions))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
