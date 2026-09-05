using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SiteModeOptions _options;
    private readonly ISiteModeRegistry _siteModeRegistry;

    public SiteModeMiddleware(
        RequestDelegate next,
        SiteModeOptions options,
        ISiteModeRegistry siteModeRegistry)
    {
        _next = next;
        _options = options;
        _siteModeRegistry = siteModeRegistry;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = NormalizeHost(context.Request.Host.Host);
        var isProfessionalDomain = _options.ProfessionalDomains.Contains(host);
        var isDorksAndDiceDomain = _options.DorksAndDiceDomains.Contains(host);
        var hasTrustedAccess = TrustedAccessEvaluator.IsAuthorized(context, _options);
        var hasDeveloperAccess = hasTrustedAccess
            && context.User.Identity?.IsAuthenticated == true
            && context.User.IsInRole(AccountRoles.Dev);

        var previewModeValue = GetTrustedPreviewModeValue(context);
        var resolution = ResolveRequestMode(
            isProfessionalDomain,
            isDorksAndDiceDomain,
            hasTrustedAccess,
            previewModeValue);
        var legacySiteMode = ToLegacySiteMode(resolution.ActiveMode, resolution.FrameworkState);
        var hasEditorPreviewAccess = hasTrustedAccess
            && CanPreviewUnlisted(context.User, resolution.ActiveMode);
        var includeUnlistedArticles = GetIncludeUnlistedArticles(context, hasEditorPreviewAccess);
        var sourceSelection = GetEnabledContentSources(context, hasDeveloperAccess);

        // Route ownership is still an enum-based compatibility boundary. A newly registered
        // mode without a legacy mapping therefore projects to Unassigned and fails closed
        // until route ownership is migrated to stable mode ids.
        var isAllowedInMode = SiteRouteOwnership.IsAllowedInMode(context.Request.Path, legacySiteMode);

        context.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = resolution.ActiveMode,
            FrameworkState = resolution.FrameworkState,
            IsProfessionalDomain = isProfessionalDomain,
            IsDorksAndDiceDomain = isDorksAndDiceDomain,
            HasTrustedAccess = hasTrustedAccess,
            IsDevelopmentPreview = hasDeveloperAccess,
            IncludeUnlistedArticles = includeUnlistedArticles,
            HasContentSourceOverride = sourceSelection.HasOverride,
            EnabledContentSources = sourceSelection.EnabledSources,
            DevelopmentPreviewRouteRestrictionMismatch = hasDeveloperAccess && !isAllowedInMode
        };

        if (!hasDeveloperAccess && !isAllowedInMode)
        {
            context.Request.Path = "/Home/NotFoundPage";
            await _next(context);
            return;
        }

        await _next(context);
    }

    private static string NormalizeHost(string host)
    {
        var normalizedHost = host.ToLowerInvariant();
        return normalizedHost.StartsWith("www.", StringComparison.Ordinal)
            ? normalizedHost[4..]
            : normalizedHost;
    }

    private RequestModeResolution ResolveRequestMode(
        bool isProfessionalDomain,
        bool isDorksAndDiceDomain,
        bool hasTrustedAccess,
        string previewModeValue)
    {
        if (hasTrustedAccess)
        {
            _siteModeRegistry.TryGetById(previewModeValue, out var previewMode);
            return new RequestModeResolution(previewMode, FrameworkRuntimeStates.TrustedPreview);
        }

        if (isProfessionalDomain)
        {
            return new RequestModeResolution(
                _siteModeRegistry.GetByLegacyMode(SiteMode.Professional),
                FrameworkState: null);
        }

        if (isDorksAndDiceDomain)
        {
            return new RequestModeResolution(
                _siteModeRegistry.GetByLegacyMode(SiteMode.DorksAndDice),
                FrameworkState: null);
        }

        return new RequestModeResolution(
            ActiveMode: null,
            FrameworkRuntimeStates.Fallback);
    }

    private string GetTrustedPreviewModeValue(HttpContext context)
    {
        var previewModeValue = context.Request.Cookies[SiteModeValues.DevelopmentSiteModeCookie]
            ?? FrameworkRuntimeStates.TrustedPreview.Id;

        if (string.Equals(
                previewModeValue,
                FrameworkRuntimeStates.TrustedPreview.Id,
                StringComparison.Ordinal))
        {
            return previewModeValue;
        }

        return _siteModeRegistry.TryGetById(previewModeValue, out _)
            ? previewModeValue
            : FrameworkRuntimeStates.TrustedPreview.Id;
    }

    private static SiteMode ToLegacySiteMode(
        SiteModeDefinition? activeMode,
        FrameworkRuntimeStateDefinition? frameworkState)
    {
        if (activeMode is not null)
        {
            return activeMode.LegacyMode ?? SiteMode.Unassigned;
        }

        return frameworkState?.LegacyMode ?? SiteMode.Unassigned;
    }

    private static bool GetIncludeUnlistedArticles(HttpContext context, bool hasEditorPreviewAccess)
    {
        return hasEditorPreviewAccess
            && string.Equals(context.Request.Cookies[SiteModeValues.IncludeUnlistedCookie], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool HasOverride, IReadOnlySet<string> EnabledSources) GetEnabledContentSources(
        HttpContext context,
        bool hasDeveloperAccess)
    {
        if (!hasDeveloperAccess)
        {
            return (false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var rawValue = context.Request.Cookies[SiteModeValues.EnabledContentSourcesCookie];
        if (rawValue is null)
        {
            return (false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        if (string.Equals(rawValue, SiteModeValues.NoContentSourcesCookieValue, StringComparison.Ordinal))
        {
            return (true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return (
            true,
            new HashSet<string>(
                rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase));
    }

    private static bool CanPreviewUnlisted(
        System.Security.Claims.ClaimsPrincipal user,
        SiteModeDefinition? activeMode)
    {
        if (user.Identity?.IsAuthenticated != true || activeMode is null)
        {
            return false;
        }

        if (user.IsInRole(AccountRoles.Admin))
        {
            return true;
        }

        return user.HasClaim(
            AccountClaimTypes.ScopedRole,
            $"{activeMode.Id}:{ScopedAccountRoles.Editor}");
    }

    private readonly record struct RequestModeResolution(
        SiteModeDefinition? ActiveMode,
        FrameworkRuntimeStateDefinition? FrameworkState);
}
