namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Central limits for browser-authored content. These are intentionally conservative so malformed or
/// unexpectedly large requests are rejected before they reach rendering or persistence layers.
/// </summary>
public static class ContentInputPolicy
{
    public const int MaxBodyLength = 262_144;
    public const int MaxMetadataJsonLength = 65_536;
    public const int MaxKeyLength = 120;
    public const int MaxTagTextLength = 4_096;
    public const int MaxModesTextLength = 1_024;
    public const int MaxTitleLength = 256;
    public const int MaxSummaryLength = 4_096;
    public const int MaxShortTextLength = 1_024;
    public const int MaxUrlLength = 2_048;
    public const int MaxTags = 32;
    public const int MaxTagLength = 64;
    public const int MaxHighlights = 64;
    public const int MaxHighlightLength = 4_096;
    public const int MaxInfoItems = 64;
    public const int MaxAuthoringRequestBytes = 524_288;
}
