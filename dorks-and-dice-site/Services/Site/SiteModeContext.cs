using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeContext
{
    private SiteMode? _legacySiteModeOverride;

    public const string HttpContextItemKey = "SiteModeContext";
    public const string RouteResolutionTitleItemKey = "RouteResolutionTitle";
    public const string RouteResolutionMessageItemKey = "RouteResolutionMessage";

    /// <summary>
    /// The normal hosted site mode selected for this request, either by deployment host mapping
    /// or as the preview target of a synthetic Development request.
    /// </summary>
    public SiteModeDefinition? ActiveMode { get; init; }

    /// <summary>
    /// Optional framework runtime state. Framework fallback remains a non-mode state. Development
    /// is represented by a SyntheticSiteModeDefinition and can coexist with ActiveMode because the
    /// latter is the normal-mode preview target, not the request's control-plane identity.
    /// </summary>
    public FrameworkRuntimeStateDefinition? FrameworkState { get; init; }

    public SyntheticSiteModeDefinition? SyntheticMode => FrameworkState as SyntheticSiteModeDefinition;
    public string? ActiveModeId => ActiveMode?.Id;
    public string? RuntimeModeId => SyntheticMode?.Id ?? ActiveModeId;
    public bool IsSyntheticMode => SyntheticMode is not null;
    public bool IsFrameworkFallback =>
        FrameworkState is not SyntheticSiteModeDefinition
        && FrameworkState?.LegacyMode == global::dorks_and_dice_site.Models.Site.SiteMode.Unassigned;

    // Compatibility name retained while callers migrate to SyntheticMode/IsSyntheticMode.
    public bool IsTrustedPreview =>
        SyntheticMode?.LegacyMode == global::dorks_and_dice_site.Models.Site.SiteMode.Development;

    /// <summary>
    /// Temporary compatibility projection for consumers that still use the SiteMode enum.
    /// Synthetic mode identity takes precedence over its selected normal preview target. A normal
    /// mode with no legacy enum mapping intentionally projects to Unassigned so legacy consumers
    /// fail closed until they are migrated.
    /// </summary>
    public SiteMode SiteMode
    {
        get
        {
            if (SyntheticMode is not null)
            {
                return SyntheticMode.LegacyMode;
            }

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

    public bool HasTrustedAccess { get; init; }
    public bool IsAssignedDomain => ActiveMode is not null || SyntheticMode is not null || HasTrustedAccess;

    // Compatibility name: this means the authenticated user can use developer controls while
    // Development is active. It is not the Development mode identity itself.
    public bool IsDevelopmentPreview { get; init; }
    public bool IncludeUnlistedArticles { get; init; }
    public bool HasContentSourceOverride { get; init; }
    public IReadOnlySet<string> EnabledContentSources { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool DevelopmentPreviewRouteRestrictionMismatch { get; init; }
}
