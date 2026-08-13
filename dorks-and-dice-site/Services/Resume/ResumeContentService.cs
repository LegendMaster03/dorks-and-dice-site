using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Resume;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Services.Resume;

public class ResumeContentService : IResumeContentService
{
    private static readonly string[] ExperienceOrder =
    [
        "seniorproject",
        "experiencecaspenterprises",
        "experiencetechnologyservices",
        "experiencecybersecurityteam",
        "experiencesimlab",
        "experiencewiredworks",
        "skyblivion",
        "skywind"
    ];

    private static readonly string[] ProjectOrder =
    [
        "xngine",
        "pythonfinanceanalytics",
        "personalmultimodewebsite",
        "seniorproject",
        "directedindependentstudy",
        "skyblivion",
        "skywind",
        "simlabexpo",
        "dndtools"
    ];

    private readonly IContentCatalogService _contentCatalogService;

    public ResumeContentService(IContentCatalogService contentCatalogService)
    {
        _contentCatalogService = contentCatalogService;
    }

    public async Task<ResumeViewModel> GetResumePageAsync(CancellationToken cancellationToken = default)
    {
        var model = ResumePageContentBuilder.Build();
        model.ExperienceItems = SortBySlugOrder(
            await _contentCatalogService.GetByContextAsync(
            ContentTags.Experience,
            SiteMode.Professional,
            includeUnlisted: false,
            cancellationToken),
            ExperienceOrder,
            ContentTags.Experience);
        model.ProjectItems = SortBySlugOrder(
            await _contentCatalogService.GetByContextAsync(
            ContentTags.Project,
            SiteMode.Professional,
            includeUnlisted: false,
            cancellationToken),
            ProjectOrder,
            ContentTags.Project);
        return model;
    }

    private static List<ContentItem> SortBySlugOrder(
        IReadOnlyList<ContentItem> items,
        IReadOnlyList<string> slugOrder,
        string contextTag)
    {
        var order = slugOrder
            .Select((slug, index) => new { slug, index })
            .ToDictionary(item => item.slug, item => item.index, StringComparer.OrdinalIgnoreCase);

        return items
            .OrderBy(item => order.TryGetValue(item.Slug, out var index) ? index : int.MaxValue)
            .ThenBy(item => item.GetTitle(contextTag), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
