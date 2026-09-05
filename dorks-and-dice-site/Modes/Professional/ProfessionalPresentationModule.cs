using System.Text.Json;
using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Modes.Professional;

public sealed class ProfessionalPresentationModule : ISiteModePresentationModule
{
    public string PresentationKey => BuiltInSiteModes.Professional.Id;

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

    public string? GetDefaultMetaImagePath()
    {
        return "/site-modes/professional/images/profile/kyle-headshot.jpg";
    }

    public string? GetStructuredDataJson(string canonicalOrigin)
    {
        return JsonSerializer.Serialize(new
        {
            @context = "https://schema.org",
            @type = "ProfilePage",
            mainEntity = new
            {
                @type = "Person",
                name = "Kyle W. Barnett",
                url = $"{canonicalOrigin}/",
                image = $"{canonicalOrigin}/site-modes/professional/images/profile/kyle-headshot.jpg",
                jobTitle = "Information Science Graduate",
                sameAs = new[]
                {
                    "https://www.linkedin.com/in/kyle-barnett03/",
                    "https://github.com/LegendMaster03"
                }
            }
        });
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
            EmptyStateText = "No listed articles are available for this site mode yet."
        };
    }
}
