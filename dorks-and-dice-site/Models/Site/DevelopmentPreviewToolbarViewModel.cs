namespace dorks_and_dice_site.Models.Site;

public sealed class DevelopmentPreviewToolbarViewModel
{
    public SiteMode SelectedMode { get; init; }
    public bool IncludeUnlistedArticles { get; init; }
    public bool RouteRestrictionMismatch { get; init; }
    public string ReturnUrl { get; init; } = "/";
    public List<DevelopmentContentSourceToggleViewModel> ContentSources { get; init; } = [];
}

public sealed class DevelopmentContentSourceToggleViewModel
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
}
