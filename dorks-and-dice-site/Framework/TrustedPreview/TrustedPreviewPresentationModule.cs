using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Framework.TrustedPreview;

public sealed class TrustedPreviewPresentationModule : ISiteModePresentationModule
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

    public string? GetDefaultMetaImagePath() => null;

    public string? GetStructuredDataJson(string canonicalOrigin) => null;

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
