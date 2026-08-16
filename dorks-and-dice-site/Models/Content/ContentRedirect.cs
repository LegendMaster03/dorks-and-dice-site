namespace dorks_and_dice_site.Models.Content;

public static class ContentRouteNamespaces
{
    public const string Resume = "resume";
    public const string Articles = "articles";

    public static IReadOnlyList<string> FromTags(IEnumerable<string> tags)
    {
        var namespaces = new List<string>();
        foreach (var tag in tags)
        {
            if (string.Equals(tag, ContentTags.Article, StringComparison.OrdinalIgnoreCase))
            {
                AddDistinct(namespaces, Articles);
            }
            else if (string.Equals(tag, ContentTags.Project, StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, ContentTags.Experience, StringComparison.OrdinalIgnoreCase))
            {
                AddDistinct(namespaces, Resume);
            }
        }

        return namespaces;
    }

    public static bool IsKnown(string routeNamespace) => routeNamespace is Resume or Articles;

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }
}

public sealed class ContentRedirectTarget
{
    public required string ContentKey { get; init; }
}
