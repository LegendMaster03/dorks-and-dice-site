using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

public sealed class UnassignedPresentationModule : ISiteModePresentationModule
{
    public SiteMode SiteMode => SiteMode.Unassigned;

    public string GetTitleSuffix()
    {
        return "Unassigned Site";
    }

    public string GetDefaultMetaDescription()
    {
        return "This domain has not been assigned to a site mode.";
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation()
    {
        return new ArticlesIndexPresentationViewModel
        {
            MetaTitle = "Articles - Unassigned Site",
            MetaDescription = "This domain has not been assigned to a site mode.",
            Eyebrow = "Articles",
            Title = "Articles",
            Description = "This domain has not been assigned to a site mode.",
            EmptyStateText = "No articles are available for this unassigned site mode."
        };
    }
}
