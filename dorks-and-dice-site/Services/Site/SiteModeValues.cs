using System.Text;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public static class SiteModeValues
{
    public const string DevelopmentSiteModeCookie = "DevelopmentPreviewSiteMode";
    public const string IncludeUnlistedCookie = "DevelopmentIncludeUnlistedArticles";
    public const string EnabledContentSourcesCookie = "DevelopmentEnabledContentSources";
    public const string NoContentSourcesCookieValue = "__none__";
    public const string DorksAndDiceModeValue = "dorks-and-dice";
    public const string ProfessionalModeValue = "professional";
    public const string DevelopmentModeValue = "development";

    public static bool IsEditorMode(SiteMode mode) =>
        BuiltInSiteModes.TryGetByLegacyMode(mode, out _);

    public static string ToModeValue(SiteMode mode)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(mode, out var modeDefinition))
        {
            return modeDefinition!.Id;
        }

        if (FrameworkRuntimeStates.TryGetByLegacyMode(mode, out var runtimeState))
        {
            return runtimeState!.Id;
        }

        return ToKebabCase(mode.ToString());
    }

    public static string ToDisplayName(SiteMode mode)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(mode, out var modeDefinition))
        {
            return modeDefinition!.DisplayName;
        }

        if (FrameworkRuntimeStates.TryGetByLegacyMode(mode, out var runtimeState))
        {
            return runtimeState!.DisplayName;
        }

        return SplitPascalCase(mode.ToString());
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index += 1)
        {
            var current = value[index];
            if (index > 0
                && char.IsUpper(current)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1]))))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static string SplitPascalCase(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index += 1)
        {
            var current = value[index];
            if (index > 0
                && char.IsUpper(current)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
