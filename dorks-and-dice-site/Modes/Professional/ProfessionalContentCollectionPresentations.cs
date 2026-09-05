using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Modes.Professional;

public sealed class ProfessionalExperienceCollectionPresentation : IContentCollectionPresentation
{
    public string Key => "professional-experience";
    public string ViewPath => "~/Views/SiteModes/Professional/Resume/Components/ExperienceCollection.cshtml";
}

public sealed class ProfessionalProjectCollectionPresentation : IContentCollectionPresentation
{
    public string Key => "professional-projects";
    public string ViewPath => "~/Views/SiteModes/Professional/Resume/Components/ProjectCollection.cshtml";
}
