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
        var isDevelopmentHost = _options.DevelopmentHosts.Contains(host);
        var previewModeValue = GetDevelopmentPreviewModeValue(context);
        var includeUnlistedArticles = GetIncludeUnlistedArticles(context, isDevelopmentHost);
        var siteMode = ResolveSiteMode(isProfessionalDomain, isDevelopmentHost, previewModeValue);
        var pageMode = ResolvePageMode(context.Request.Path, siteMode);
        var isAllowedInMode = SiteRouteOwnership.IsAllowedInMode(context.Request.Path, siteMode);

        context.Items[SiteModeContext.HttpContextItemKey] = new SiteModeContext
        {
            SiteMode = siteMode,
            PageMode = pageMode,
            IsProfessionalDomain = isProfessionalDomain,
            IsDorksAndDiceDomain = isDorksAndDiceDomain,
            IsDevelopmentPreview = isDevelopmentHost,
            IncludeUnlistedArticles = includeUnlistedArticles,
            DevelopmentPreviewRouteRestrictionMismatch = isDevelopmentHost && !isAllowedInMode
        };

        if (!isDevelopmentHost && !isAllowedInMode)
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

    private static SiteMode ResolveSiteMode(bool isProfessionalDomain, bool isDevelopmentHost, string previewModeValue)
    {
        if (isDevelopmentHost)
        {
            return previewModeValue switch
            {
                SiteModeValues.ProfessionalModeValue => SiteMode.Professional,
                SiteModeValues.DevelopmentModeValue => SiteMode.Development,
                _ => SiteMode.DorksAndDice
            };
        }

        return isProfessionalDomain ? SiteMode.Professional : SiteMode.Unassigned;
    }

    private static SiteMode ResolvePageMode(PathString path, SiteMode siteMode)
    {
        if (siteMode == SiteMode.Development)
        {
            return SiteRouteOwnership.GetSingleOwningMode(path) ?? SiteMode.Development;
        }

        return siteMode;
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

    private static bool GetIncludeUnlistedArticles(HttpContext context, bool isDevelopmentHost)
    {
        var includeUnlistedArticles = isDevelopmentHost
            && string.Equals(context.Request.Cookies[SiteModeValues.IncludeUnlistedCookie], "true", StringComparison.OrdinalIgnoreCase);

        return includeUnlistedArticles;
    }

    private static bool IsKnownDevelopmentPreviewModeValue(string? value)
    {
        return value is SiteModeValues.DorksAndDiceModeValue
            or SiteModeValues.ProfessionalModeValue
            or SiteModeValues.DevelopmentModeValue;
    }
}
