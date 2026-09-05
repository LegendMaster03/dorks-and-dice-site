namespace dorks_and_dice_site.Models.Site;

public sealed class DevelopmentPreviewToolbarViewModel
{
    public string SelectedModeId { get; init; } = string.Empty;
    public List<DevelopmentPreviewModeOptionViewModel> ModeOptions { get; init; } = [];
    public bool IncludeUnlistedArticles { get; init; }
    public bool RouteRestrictionMismatch { get; init; }
    public bool CanUseDeveloperTools { get; init; }
    public string ReturnUrl { get; init; } = "/";
    public List<DevelopmentContentSourceToggleViewModel> ContentSources { get; init; } = [];
}

public sealed class DevelopmentPreviewModeOptionViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class DevelopmentContentSourceToggleViewModel
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
}
