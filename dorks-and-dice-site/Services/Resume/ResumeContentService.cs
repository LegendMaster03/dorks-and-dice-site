using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Resume;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Services.Resume;

public class ResumeContentService : IResumeContentService
{
    private readonly IContentCatalogService _contentCatalogService;

    public ResumeContentService(IContentCatalogService contentCatalogService)
    {
        _contentCatalogService = contentCatalogService;
    }

    public async Task<ResumeViewModel> GetResumePageAsync(CancellationToken cancellationToken = default)
    {
        var model = ResumePageContentBuilder.Build();
        model.ExperienceItems = (await _contentCatalogService.GetByContextAsync(
            ContentTags.Experience,
            SiteMode.Professional,
            includeUnlisted: false,
            cancellationToken)).ToList();
        model.ProjectItems = (await _contentCatalogService.GetByContextAsync(
            ContentTags.Project,
            SiteMode.Professional,
            includeUnlisted: false,
            cancellationToken)).ToList();
        return model;
    }
}
