using System.Text.Json;
using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Modes.Professional;

/// <summary>
/// Professional-mode metadata, profile links, and structured-data presentation.
/// This site identity and media belong to the Professional module, not the
/// generic reusable framework.
/// </summary>
public sealed class ProfessionalPresentationModule : ISiteModePresentationModule
{
    private const string HeadshotPath =
        "/content/media/21a9d1fde7ce4c8db49140ec3666641b/kyle-headshot.jpg";

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
        return HeadshotPath;
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
                image = $"{canonicalOrigin}{HeadshotPath}",
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
