using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

public sealed class DorksAndDicePresentationModule : ISiteModePresentationModule
{
    public SiteMode SiteMode => SiteMode.DorksAndDice;

    public string GetTitleSuffix()
    {
        return "Dorks & Dice";
    }

    public string GetDefaultMetaDescription()
    {
        return "Dorks & Dice community front door for campaigns, tools, and updates.";
    }

    public string GetFaviconPath()
    {
        return "~/site-modes/dorks-and-dice/images/favicon.svg";
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation()
    {
        return new ArticlesIndexPresentationViewModel
        {
            MetaTitle = "Articles - Dorks & Dice",
            MetaDescription = "Long-form write-ups, technical investigations, and puzzle walkthroughs by Kyle Barnett.",
            Eyebrow = "Articles",
            Title = "Long-Form Write-Ups",
            Description = "Technical investigations, puzzle walkthroughs, and narrative project notes.",
            EmptyStateText = "No listed articles are available for this site mode yet."
        };
    }
}
