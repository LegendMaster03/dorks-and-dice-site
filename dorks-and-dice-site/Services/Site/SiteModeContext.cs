using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeContext
{
    public const string HttpContextItemKey = "SiteModeContext";
    public const string RouteResolutionTitleItemKey = "RouteResolutionTitle";
    public const string RouteResolutionMessageItemKey = "RouteResolutionMessage";

    public SiteMode SiteMode { get; init; } = SiteMode.Unassigned;
    public bool IsProfessionalDomain { get; init; }
    public bool IsDorksAndDiceDomain { get; init; }
    public bool IsAssignedDomain => IsProfessionalDomain || IsDorksAndDiceDomain || IsDevelopmentPreview;
    public bool IsDevelopmentPreview { get; init; }
    public bool IncludeUnlistedArticles { get; init; }
    public bool HasContentSourceOverride { get; init; }
    public IReadOnlySet<string> EnabledContentSources { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool DevelopmentPreviewRouteRestrictionMismatch { get; init; }
}
