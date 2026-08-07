namespace dorks_and_dice_site.Models.Site;

public sealed class SiteModeArchitectureSummaryViewModel
{
    public IReadOnlyList<SiteModeSummaryRowViewModel> Modes { get; init; } = [];
    public IReadOnlyList<RouteOwnershipProbeViewModel> RouteProbes { get; init; } = [];
}
