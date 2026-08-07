using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

public sealed class ProfessionalPresentationModule : ISiteModePresentationModule
{
    public SiteMode SiteMode => SiteMode.Professional;

    public string GetTitleSuffix()
    {
        return "Kyle Barnett";
    }

    public string GetDefaultMetaDescription()
    {
        return "Kyle Barnett's resume, experience, and selected projects.";
    }

    public string GetFaviconPath()
    {
        return "~/site-modes/professional/images/favicon.svg";
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation()
    {
        return new ArticlesIndexPresentationViewModel
        {
            MetaTitle = "Articles - Kyle Barnett",
            MetaDescription = "Long-form write-ups, technical investigations, and puzzle walkthroughs by Kyle Barnett.",
            Eyebrow = "Articles",
            Title = "Long-Form Write-Ups",
            Description = "Technical investigations, puzzle walkthroughs, and narrative project notes.",
            EmptyStateText = "No listed articles are available for this site mode yet.",
            ForceProfessionalFilter = true
        };
    }
}
