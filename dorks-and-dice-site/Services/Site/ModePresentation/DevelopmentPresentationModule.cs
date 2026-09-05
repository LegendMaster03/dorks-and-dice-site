using dorks_and_dice_site.Models.Articles;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

/// <summary>
/// Compatibility presentation for Trusted Preview when no normal site mode is selected.
/// The class/file name remains legacy until physical ownership consolidation.
/// </summary>
public sealed class DevelopmentPresentationModule : ISiteModePresentationModule
{
    public string PresentationKey => FrameworkRuntimeStates.TrustedPreview.Id;

    public string GetTitleSuffix()
    {
        return "Development Preview";
    }

    public string GetDefaultMetaDescription()
    {
        return "Local development preview for mode-aware site content.";
    }

    public string GetFaviconPath()
    {
        return "~/site-modes/development/images/favicon.svg";
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation()
    {
        return new ArticlesIndexPresentationViewModel
        {
            MetaTitle = "Articles - Development Preview",
            MetaDescription = "Local development preview for long-form write-ups, technical investigations, and puzzle walkthroughs.",
            Eyebrow = "Articles",
            Title = "Long-Form Write-Ups",
            Description = "Technical investigations, puzzle walkthroughs, and narrative project notes.",
            EmptyStateText = "No listed articles are available for this site mode yet."
        };
    }
}
