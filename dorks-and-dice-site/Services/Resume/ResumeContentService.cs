using dorks_and_dice_site.Models.Resume;

namespace dorks_and_dice_site.Services.Resume;

public class ResumeContentService : IResumeContentService
{
    public ResumeViewModel GetResumePage() => ResumePageContentBuilder.Build();
}
