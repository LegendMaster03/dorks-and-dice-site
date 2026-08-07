namespace dorks_and_dice_site.Models.Site;

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
