using System.Text.RegularExpressions;

namespace dorks_and_dice_site.Services.Content;

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
        @"/content/media/(?<key>[0-9a-f]{32})/[A-Za-z0-9_-]+\.(?:jpg|jpeg|png|webp|gif)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ManagedMediaUrlPattern();
}
