namespace dorks_and_dice_site.Models.Site;

/// <summary>
/// View models used only by the Professional portfolio's public architecture-summary component.
/// They retain their existing namespace while their physical ownership moves with the plugin.
/// </summary>
public sealed class SiteModeArchitectureSummaryViewModel
{
    public IReadOnlyList<SiteModeSummaryRowViewModel> Modes { get; init; } = [];
    public IReadOnlyList<RouteOwnershipProbeViewModel> RouteProbes { get; init; } = [];
}

public sealed class SiteModeSummaryRowViewModel
{
    public SiteMode SiteMode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PublicIdentity { get; init; } = string.Empty;
    public IReadOnlyList<string> Hosts { get; init; } = [];
    public string Homepage { get; init; } = string.Empty;
    public IReadOnlyList<string> Stylesheets { get; init; } = [];
    public IReadOnlyList<string> BrandingPartials { get; init; } = [];
    public string RouteOwnership { get; init; } = string.Empty;
    public string AssetOwnership { get; init; } = string.Empty;
    public string ArticleBehavior { get; init; } = string.Empty;
}

public sealed class RouteOwnershipProbeViewModel
{
    public string Path { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public IReadOnlyDictionary<SiteMode, bool> AllowedByMode { get; init; } = new Dictionary<SiteMode, bool>();
}
