using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeContext
{
    private SiteMode? _legacySiteModeOverride;

    public const string HttpContextItemKey = "SiteModeContext";
    public const string RouteResolutionTitleItemKey = "RouteResolutionTitle";
    public const string RouteResolutionMessageItemKey = "RouteResolutionMessage";

    /// <summary>
    /// The normal hosted site mode selected for this request, either by deployment host
    /// mapping or as a Trusted Preview target. Framework fallback and Trusted Preview are
    /// represented separately by <see cref="FrameworkState"/>.
    /// </summary>
    public SiteModeDefinition? ActiveMode { get; init; }

    /// <summary>
    /// Optional framework state layered onto the request. Trusted Preview can coexist with
    /// an ActiveMode because it previews a normal site mode; fallback is used when no normal
    /// mode is selected.
    /// </summary>
    public FrameworkRuntimeStateDefinition? FrameworkState { get; init; }

    public string? ActiveModeId => ActiveMode?.Id;
    public bool IsFrameworkFallback =>
        FrameworkState?.LegacyMode == global::dorks_and_dice_site.Models.Site.SiteMode.Unassigned;
    public bool IsTrustedPreview =>
        FrameworkState?.LegacyMode == global::dorks_and_dice_site.Models.Site.SiteMode.Development;

    /// <summary>
    /// Temporary compatibility projection for consumers that still use the SiteMode enum.
    /// New code should prefer ActiveMode/ActiveModeId plus FrameworkState. A normal mode with
    /// no legacy enum mapping intentionally projects to Unassigned so legacy consumers fail
    /// closed until they are migrated.
    /// </summary>
    public SiteMode SiteMode
    {
        get
        {
            if (ActiveMode is not null)
            {
                return ActiveMode.LegacyMode
                    ?? global::dorks_and_dice_site.Models.Site.SiteMode.Unassigned;
            }

            return FrameworkState?.LegacyMode
                ?? _legacySiteModeOverride
                ?? global::dorks_and_dice_site.Models.Site.SiteMode.Unassigned;
        }
        init => _legacySiteModeOverride = value;
    }

    public bool IsProfessionalDomain { get; init; }
    public bool IsDorksAndDiceDomain { get; init; }
    public bool HasTrustedAccess { get; init; }
    public bool IsAssignedDomain => IsProfessionalDomain || IsDorksAndDiceDomain || HasTrustedAccess;

    // Compatibility name: this currently means the authenticated user can use developer
    // controls while in Trusted Preview, not that Development is a normal site mode.
    public bool IsDevelopmentPreview { get; init; }
    public bool IncludeUnlistedArticles { get; init; }
    public bool HasContentSourceOverride { get; init; }
    public IReadOnlySet<string> EnabledContentSources { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool DevelopmentPreviewRouteRestrictionMismatch { get; init; }
}
