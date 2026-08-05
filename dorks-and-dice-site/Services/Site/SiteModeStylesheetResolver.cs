using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeStylesheetResolver : ISiteModeStylesheetResolver
{
    private const string ProfessionalStylesheetPath = "~/site-modes/professional/css/site.css";
    private const string DorksAndDiceStylesheetPath = "~/site-modes/dorks-and-dice/css/site.css";
    private const string DevelopmentStylesheetPath = "~/site-modes/development/css/site.css";

    public IReadOnlyList<string> GetStylesheetPaths(SiteMode siteMode, bool includeDevelopmentTools)
    {
        var paths = new List<string>(2);

        switch (siteMode)
        {
            case SiteMode.Professional:
                paths.Add(ProfessionalStylesheetPath);
                break;
            case SiteMode.DorksAndDice:
                paths.Add(DorksAndDiceStylesheetPath);
                break;
            case SiteMode.Development:
                paths.Add(DevelopmentStylesheetPath);
                break;
            case SiteMode.Unassigned:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(siteMode), siteMode, "Unknown site mode.");
        }

        if (includeDevelopmentTools && siteMode != SiteMode.Development)
        {
            paths.Add(DevelopmentStylesheetPath);
        }

        return paths;
    }
}
