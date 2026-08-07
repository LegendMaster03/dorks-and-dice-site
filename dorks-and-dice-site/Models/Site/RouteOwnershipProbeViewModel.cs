namespace dorks_and_dice_site.Models.Site;

public sealed class RouteOwnershipProbeViewModel
{
    public string Path { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public IReadOnlyDictionary<SiteMode, bool> AllowedByMode { get; init; } = new Dictionary<SiteMode, bool>();
}
