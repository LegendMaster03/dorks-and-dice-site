using System.Text.RegularExpressions;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Finds references to managed-media keys in authored Markdown and metadata.
/// This recognizes canonical media URLs; it does not establish whether a
/// referenced asset exists or is authorized for publication.
/// </summary>
internal static partial class ContentAssetReferenceParser
{
    public static IReadOnlySet<string> FindAssetKeys(string body, string metadataJson)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        AddMatches(keys, body);
        AddMatches(keys, metadataJson);
        return keys;
    }

    private static void AddMatches(HashSet<string> keys, string value)
    {
        foreach (Match match in ManagedMediaUrlPattern().Matches(value))
        {
            keys.Add(match.Groups["key"].Value);
        }
    }

    [GeneratedRegex(
        @"/content/media/(?<key>[0-9a-f]{32})/[A-Za-z0-9_-]+\.(?:jpg|jpeg|png|webp|gif|pdf|svg)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ManagedMediaUrlPattern();
}
