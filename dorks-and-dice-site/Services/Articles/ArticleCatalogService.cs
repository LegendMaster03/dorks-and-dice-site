using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Articles;

public class ArticleCatalogService : IArticleCatalogService
{
    private readonly ISiteModePresentationService _siteModePresentationService;

    public ArticleCatalogService(ISiteModePresentationService siteModePresentationService)
    {
        _siteModePresentationService = siteModePresentationService;
    }

    private static readonly List<ArticleItemViewModel> Articles =
    [
        new()
        {
            Title = "Freeing the Bees: Solving ConsoleVariations' Hidden Web Puzzle",
            Summary = "My third-place solve of ConsoleVariations' Free the Bees puzzle, including the visible clue path, encoded password work, browser-state investigation, and final result.",
            Category = "Technical Investigation",
            Controller = "Articles",
            Action = "FreeingTheBeesConsoleVariationsPuzzle",
            PostedDateText = "August 2026",
            ImageUrl = "~/site-modes/professional/images/articles/consolevariations-bee/ending.png",
            ImageAltText = "Completed ConsoleVariations Queen's Chamber showing the Free the Bees ending screen",
            ImageWidth = 2041,
            ImageHeight = 1220,
            Listed = false,
            VisibleInModes =
            [
                SiteMode.Professional
            ],
            Tags =
            [
                "technical-investigation",
                "puzzle",
                "write-up"
            ]
        }
    ];

    public ArticlesIndexViewModel GetIndex(SiteMode siteMode, bool includeUnlisted, bool isDevelopmentPreview)
    {
        var visibleArticles = Articles
            .Where(article => includeUnlisted || article.Listed)
            .Where(article => article.IsVisibleInMode(siteMode))
            .ToList();

        return new ArticlesIndexViewModel
        {
            Articles = visibleArticles,
            Presentation = _siteModePresentationService.GetArticlesIndexPresentation(siteMode),
            SiteMode = siteMode,
            IsDevelopmentPreview = isDevelopmentPreview,
            IncludeUnlistedActive = includeUnlisted,
            Categories = visibleArticles
                .Select(article => article.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public ArticleItemViewModel? GetByAction(string action)
    {
        return Articles.FirstOrDefault(article => string.Equals(article.Action, action, StringComparison.Ordinal));
    }
}
