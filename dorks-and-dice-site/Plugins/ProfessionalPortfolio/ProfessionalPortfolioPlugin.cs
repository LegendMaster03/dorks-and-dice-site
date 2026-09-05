using dorks_and_dice_site.Framework.Plugins;
using dorks_and_dice_site.Services.Content;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Plugins.ProfessionalPortfolio;

/// <summary>
/// Supplies the Professional portfolio/resume presentations used by the generic
/// content-collection page component. The content query remains framework-owned;
/// this plugin owns only the specialized presentation.
/// </summary>
public sealed class ProfessionalPortfolioPlugin : ISitePlugin
{
    public SitePluginManifest Manifest { get; } = new(
        Id: "professional-portfolio",
        DisplayName: "Professional Portfolio",
        Version: "1.0.0");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IContentCollectionPresentation>(
            new Presentation(
                "professional-experience",
                "~/Views/Plugins/ProfessionalPortfolio/ExperienceCollection.cshtml"));
        services.AddSingleton<IContentCollectionPresentation>(
            new Presentation(
                "professional-projects",
                "~/Views/Plugins/ProfessionalPortfolio/ProjectCollection.cshtml"));
    }

    private sealed record Presentation(string Key, string ViewPath) : IContentCollectionPresentation;
}
