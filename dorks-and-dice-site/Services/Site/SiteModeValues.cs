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
        mode is not SiteMode.Unassigned and not SiteMode.Development;

    public static string ToModeValue(SiteMode mode) => mode switch
    {
        SiteMode.DorksAndDice => DorksAndDiceModeValue,
        SiteMode.Professional => ProfessionalModeValue,
        SiteMode.Development => DevelopmentModeValue,
        SiteMode.Unassigned => "unassigned",
        _ => ToKebabCase(mode.ToString())
    };

    public static string ToDisplayName(SiteMode mode) => mode switch
    {
        SiteMode.DorksAndDice => "Dorks & Dice",
        SiteMode.Professional => "Professional",
        SiteMode.Development => "Development",
        SiteMode.Unassigned => "Unassigned",
        _ => SplitPascalCase(mode.ToString())
    };

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
