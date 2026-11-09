namespace dorks_and_dice_site.Models.Content;

/// <summary>
/// Composed database-backed homepage: the visible authored revision and
/// ordered sanitized Markdown or registered component fragments.
/// </summary>
public sealed class HomepageContentViewModel
{
    public required ContentItem Item { get; init; }
    public required IReadOnlyList<ContentPageFragment> Fragments { get; init; }
}
