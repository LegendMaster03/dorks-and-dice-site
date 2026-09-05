using dorks_and_dice_site.Models.Articles;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

/// <summary>
/// Framework fallback presentation retained under its legacy class/file name until physical
/// ownership consolidation.
/// </summary>
public sealed class UnassignedPresentationModule : ISiteModePresentationModule
{
    public string PresentationKey => FrameworkRuntimeStates.Fallback.Id;

    public string GetTitleSuffix()
    {
        return "Unassigned Site";
    }

    public string GetDefaultMetaDescription()
    {
        return "This domain has not been assigned to a site mode.";
    }

    public string GetFaviconPath()
    {
        return "~/favicon.ico";
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
