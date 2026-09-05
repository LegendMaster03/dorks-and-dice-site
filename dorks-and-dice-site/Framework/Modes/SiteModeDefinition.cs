using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Describes a normal hosted site mode. Framework fallback behavior and Trusted Preview
/// are intentionally not represented by this type.
/// </summary>
public sealed record SiteModeDefinition(
    string Id,
    string DisplayName,
    SiteMode? LegacyMode,
    string ViewFolder,
    string AssetFolder)
{
    // Normal site modes participate in the shared content and scoped-capability model.
    // These properties remain during migration so existing consumers can move to the
    // registry without introducing per-mode opt-in flags.
    public bool SupportsContent => true;
    public bool SupportsScopedEditor => true;

    /// <summary>
    /// Route prefixes owned by this mode in addition to the framework's shared mode-adaptive
    /// routes. Prefixes match both the exact path and descendants below that path.
    /// </summary>
    public IReadOnlyList<string> OwnedRoutePrefixes { get; init; } = [];

    /// <summary>
    /// Exact static-asset paths this mode may use outside its own asset folder. This is for
    /// narrow compatibility exceptions, not general cross-mode asset sharing.
    /// </summary>
    public IReadOnlyList<string> AdditionalAssetPaths { get; init; } = [];

    /// <summary>
    /// Public framework routes emitted by this mode's sitemap. Every normal content-capable
    /// mode exposes the shared home and article surfaces unless it explicitly replaces this list.
    /// </summary>
    public IReadOnlyList<string> SitemapPaths { get; init; } = ["/", "/articles"];

    /// <summary>
    /// Controls only whether the shared navigation advertises anonymous account sign-in.
    /// Account authorization remains enforced independently by the Identity subsystem.
    /// </summary>
    public bool ShowAnonymousLoginInNavigation { get; init; } = true;
}
