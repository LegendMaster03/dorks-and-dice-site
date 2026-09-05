namespace dorks_and_dice_site.Models.Content;

/// <summary>
/// Maps a content record's context tags to its canonical public route. Keeping this mapping in one
/// place prevents authoring surfaces from disagreeing about where a page can be viewed.
/// </summary>
public static class ContentPublicRoute
{
    public static string GetPath(string slug, IEnumerable<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(tags);

        var tagSet = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tagSet.Contains(ContentTags.Homepage))
        {
            return "/";
        }

        if (tagSet.Contains(ContentTags.Article))
        {
            return $"/articles/{slug}";
        }

        if (tagSet.Contains(ContentTags.Experience) && !tagSet.Contains(ContentTags.Project))
        {
            return $"/resume/{slug}?context=experience";
        }

        if (tagSet.Contains(ContentTags.Project) || tagSet.Contains(ContentTags.Experience))
        {
            return $"/resume/{slug}";
        }

        throw new InvalidOperationException(
            $"Content '{slug}' does not have a supported public-route context tag.");
    }
}
