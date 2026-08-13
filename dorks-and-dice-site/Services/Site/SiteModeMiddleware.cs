using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SiteModeOptions _options;

    public SiteModeMiddleware(RequestDelegate next, SiteModeOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = NormalizeHost(context.Request.Host.Host);
        var isProfessionalDomain = _options.ProfessionalDomains.Contains(host);
        var isDorksAndDiceDomain = _options.DorksAndDiceDomains.Contains(host);
        var isDevelopmentAccess = DevelopmentAccessEvaluator.IsAuthorized(context, _options);
        var previewModeValue = GetDevelopmentPreviewModeValue(context);
        var includeUnlistedArticles = GetIncludeUnlistedArticles(context, isDevelopmentAccess);
        var sourceSelection = GetEnabledContentSources(context, isDevelopmentAccess);
        var siteMode = ResolveSiteMode(isProfessionalDomain, isDorksAndDiceDomain, isDevelopmentAccess, previewModeValue);
        var isAllowedInMode = SiteRouteOwnership.IsAllowedInMode(context.Request.Path, siteMode);

        context.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            SiteMode = siteMode,
            IsProfessionalDomain = isProfessionalDomain,
            IsDorksAndDiceDomain = isDorksAndDiceDomain,
            IsDevelopmentPreview = isDevelopmentAccess,
            IncludeUnlistedArticles = includeUnlistedArticles,
            HasContentSourceOverride = sourceSelection.HasOverride,
            EnabledContentSources = sourceSelection.EnabledSources,
            DevelopmentPreviewRouteRestrictionMismatch = isDevelopmentAccess && !isAllowedInMode
        };

        if (!isDevelopmentAccess && !isAllowedInMode)
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

    private static SiteMode ResolveSiteMode(bool isProfessionalDomain, bool isDorksAndDiceDomain, bool isDevelopmentAccess, string previewModeValue)
    {
        if (isDevelopmentAccess)
        {
            return previewModeValue switch
            {
                SiteModeValues.DorksAndDiceModeValue => SiteMode.DorksAndDice,
                SiteModeValues.ProfessionalModeValue => SiteMode.Professional,
                SiteModeValues.DevelopmentModeValue => SiteMode.Development,
                _ => SiteMode.Development
            };
        }

        if (isProfessionalDomain)
        {
            return SiteMode.Professional;
        }

        return isDorksAndDiceDomain ? SiteMode.DorksAndDice : SiteMode.Unassigned;
    }

    private static string GetDevelopmentPreviewModeValue(HttpContext context)
    {
        var previewModeValue = context.Request.Cookies[SiteModeValues.DevelopmentSiteModeCookie] ?? SiteModeValues.DevelopmentModeValue;
        if (!IsKnownDevelopmentPreviewModeValue(previewModeValue))
        {
            previewModeValue = SiteModeValues.DevelopmentModeValue;
        }

        return previewModeValue;
    }

    private static bool GetIncludeUnlistedArticles(HttpContext context, bool isDevelopmentAccess)
    {
        return isDevelopmentAccess
            && string.Equals(context.Request.Cookies[SiteModeValues.IncludeUnlistedCookie], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool HasOverride, IReadOnlySet<string> EnabledSources) GetEnabledContentSources(
        HttpContext context,
        bool isDevelopmentAccess)
    {
        if (!isDevelopmentAccess)
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

    private static bool IsKnownDevelopmentPreviewModeValue(string? value)
    {
        return value is SiteModeValues.DorksAndDiceModeValue
            or SiteModeValues.ProfessionalModeValue
            or SiteModeValues.DevelopmentModeValue;
    }
}
