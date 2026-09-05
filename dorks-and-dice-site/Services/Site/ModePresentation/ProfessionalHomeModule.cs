using dorks_and_dice_site.Services.Resume;

namespace dorks_and_dice_site.Services.Site.ModePresentation;

public sealed class ProfessionalHomeModule : ISiteModeHomeModule
{
    private readonly IResumeContentService _resumeContentService;

    public ProfessionalHomeModule(IResumeContentService resumeContentService)
    {
        _resumeContentService = resumeContentService;
    }

    public string HomeKey => BuiltInSiteModes.Professional.Id;

    public async Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default)
    {
        return new SiteModeHomeResult(
            "~/Views/SiteModes/Professional/Home.cshtml",
            await _resumeContentService.GetResumePageAsync(cancellationToken));
    }
}
