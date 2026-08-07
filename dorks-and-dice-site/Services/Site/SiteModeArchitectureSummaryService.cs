using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeArchitectureSummaryService : ISiteModeArchitectureSummaryService
{
    // Intentionally fixed for the public project page. Do not switch to Enum.GetValues;
    // future internal modes should not appear here unless they are safe public examples.
    private static readonly SiteMode[] PublicExampleModes =
    [
        SiteMode.Professional,
        SiteMode.DorksAndDice,
        SiteMode.Development,
        SiteMode.Unassigned
    ];

    private readonly SiteModeOptions _options;
    private readonly ISiteModePartialResolver _partialResolver;
    private readonly ISiteModeStylesheetResolver _stylesheetResolver;

    public SiteModeArchitectureSummaryService(
        SiteModeOptions options,
        ISiteModePartialResolver partialResolver,
        ISiteModeStylesheetResolver stylesheetResolver)
    {
        _options = options;
        _partialResolver = partialResolver;
        _stylesheetResolver = stylesheetResolver;
    }

    public SiteModeArchitectureSummaryViewModel GetSummary()
    {
        return new SiteModeArchitectureSummaryViewModel
        {
            Modes = PublicExampleModes.Select(BuildModeSummary).ToList(),
            RouteProbes = BuildRouteProbes()
        };
    }

    private SiteModeSummaryRowViewModel BuildModeSummary(SiteMode siteMode)
    {
        return new SiteModeSummaryRowViewModel
        {
            SiteMode = siteMode,
            Name = GetModeName(siteMode),
            PublicIdentity = GetPublicIdentityDescription(siteMode),
            Hosts = GetHosts(siteMode),
            Homepage = GetHomepageDescription(siteMode),
            Stylesheets = _stylesheetResolver.GetStylesheetPaths(siteMode, includeDevelopmentTools: false),
            BrandingPartials =
            [
                _partialResolver.GetBrandingPartialPath(siteMode, SiteModeBrandingPart.Header),
                _partialResolver.GetBrandingPartialPath(siteMode, SiteModeBrandingPart.Footer)
            ],
            RouteOwnership = GetRouteOwnershipDescription(siteMode),
            AssetOwnership = GetAssetOwnershipDescription(siteMode),
            ArticleBehavior = GetArticleBehaviorDescription(siteMode)
        };
    }

    private IReadOnlyList<RouteOwnershipProbeViewModel> BuildRouteProbes()
    {
        var probes = new (string Path, string Purpose)[]
        {
            ("/", "mode-adaptive home"),
            ("/resume", "professional resume surface"),
            ("/articles", "mode-aware article index"),
            ("/site-modes/professional/files/kyle-resume.pdf", "professional-owned asset"),
            ("/site-modes/dorks-and-dice/css/site.css", "community-mode asset"),
            ("/site-modes/unassigned/images/sample.png", "fallback-mode asset")
        };

        return probes
            .Select(probe => new RouteOwnershipProbeViewModel
            {
                Path = probe.Path,
                Purpose = probe.Purpose,
                AllowedByMode = PublicExampleModes.ToDictionary(
                    siteMode => siteMode,
                    siteMode => SiteRouteOwnership.IsAllowedInMode(new PathString(probe.Path), siteMode))
            })
            .ToList();
    }

    private IReadOnlyList<string> GetHosts(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => _options.ProfessionalDomains.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            SiteMode.DorksAndDice => _options.DorksAndDiceDomains.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            SiteMode.Development => _options.DevelopmentHosts.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            SiteMode.Unassigned => ["Any unmapped host"],
            _ => []
        };
    }

    private static string GetModeName(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "Professional",
            SiteMode.DorksAndDice => "Community",
            SiteMode.Development => "Development",
            SiteMode.Unassigned => "Unassigned",
            _ => siteMode.ToString()
        };
    }

    private static string GetHomepageDescription(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "Professional resume and portfolio home",
            SiteMode.DorksAndDice => "Community homepage",
            SiteMode.Development => "Selected preview mode, with development tools overlay",
            SiteMode.Unassigned => "Fallback page for unmapped hosts",
            _ => "Unknown"
        };
    }

    private static string GetPublicIdentityDescription(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "Resume, portfolio, and professional article identity",
            SiteMode.DorksAndDice => "Community-facing identity",
            SiteMode.Development => "Local-only inspection and preview tooling",
            SiteMode.Unassigned => "Fallback behavior for unmapped hosts",
            _ => "Unknown"
        };
    }

    private static string GetRouteOwnershipDescription(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "Shared routes plus /resume and Professional-owned assets",
            SiteMode.DorksAndDice => "Shared routes plus community-mode assets",
            SiteMode.Development => "Local inspection of all routes, with warnings for invalid selected-mode states",
            SiteMode.Unassigned => "Fallback home, shared system paths, shared assets, and fallback asset space",
            _ => "Unknown"
        };
    }

    private static string GetAssetOwnershipDescription(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "wwwroot/site-modes/professional",
            SiteMode.DorksAndDice => "Community-mode asset scope",
            SiteMode.Development => "wwwroot/site-modes/development",
            SiteMode.Unassigned => "Fallback asset scope plus shared static assets",
            _ => "Unknown"
        };
    }

    private static string GetArticleBehaviorDescription(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.Professional => "Shows listed Professional-eligible articles; direct access allowed for eligible unlisted articles",
            SiteMode.DorksAndDice => "Shows listed community-mode eligible articles only",
            SiteMode.Development => "Can inspect all mode-eligible articles and optionally include unlisted articles",
            SiteMode.Unassigned => "Uses fallback article presentation and mode eligibility rules",
            _ => "Unknown"
        };
    }
}
