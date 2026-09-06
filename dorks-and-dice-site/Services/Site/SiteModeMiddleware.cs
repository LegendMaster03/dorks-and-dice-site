using dorks_and_dice_site.Models.Identity;
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
        var hostedModeId = _options.ResolveModeId(host);
        var hasTrustedAccess = TrustedAccessEvaluator.IsAuthorized(context, _options);
        var hasDeveloperAccess = hasTrustedAccess
            && context.User.Identity?.IsAuthenticated == true
            && AccountRoleHierarchy.PrincipalHasGlobalRole(context.User, AccountRoles.Dev);

        var previewModeValue = GetTrustedPreviewModeValue(context);
        var resolution = ResolveRequestMode(
            hostedModeId,
            hasTrustedAccess,
            previewModeValue);
        var hasEditorPreviewAccess = hasTrustedAccess
            && CanPreviewUnlisted(context.User, resolution.ActiveMode);
        var includeUnlistedArticles = GetIncludeUnlistedArticles(context, hasEditorPreviewAccess);
        var sourceSelection = GetEnabledContentSources(context, hasDeveloperAccess);

        var isAllowedInRequest = SiteRouteOwnership.IsAllowedInRequest(
            context.Request.Path,
            resolution.ActiveMode,
            resolution.FrameworkState);
        var isAllowedInSelectedLiveMode = resolution.ActiveMode is not null
            ? SiteRouteOwnership.IsAllowedInMode(context.Request.Path, resolution.ActiveMode)
            : SiteRouteOwnership.IsAllowedInFrameworkFallback(context.Request.Path);

        context.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            ActiveMode = resolution.ActiveMode,
            FrameworkState = resolution.FrameworkState,
            HasTrustedAccess = hasTrustedAccess,
            IsDevelopmentPreview = hasDeveloperAccess,
            IncludeUnlistedArticles = includeUnlistedArticles,
            HasContentSourceOverride = sourceSelection.HasOverride,
            EnabledContentSources = sourceSelection.EnabledSources,
            DevelopmentPreviewRouteRestrictionMismatch = hasDeveloperAccess && !isAllowedInSelectedLiveMode
        };

        // Developers intentionally retain the ability to inspect a route that the selected
        // live mode would reject; the preview UI surfaces that mismatch. Other callers,
        // including trusted non-Dev editors, remain bounded by the selected mode plus the
        // Trusted Preview framework surface.
        if (!hasDeveloperAccess && !isAllowedInRequest)
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
        string? hostedModeId,
        bool hasTrustedAccess,
        string previewModeValue)
    {
        if (hasTrustedAccess)
        {
            _siteModeRegistry.TryGetById(previewModeValue, out var previewMode);
            return new RequestModeResolution(previewMode, FrameworkRuntimeStates.TrustedPreview);
        }

        if (hostedModeId is not null
            && _siteModeRegistry.TryGetById(hostedModeId, out var hostedMode))
        {
            return new RequestModeResolution(hostedMode, FrameworkState: null);
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

        return AccountRoleHierarchy.PrincipalHasGlobalRole(user, AccountRoles.Admin)
            || AccountRoleHierarchy.PrincipalHasScopedRole(
                user,
                activeMode.Id,
                ScopedAccountRoles.Editor);
    }

    private readonly record struct RequestModeResolution(
        SiteModeDefinition? ActiveMode,
        FrameworkRuntimeStateDefinition? FrameworkState);
}
