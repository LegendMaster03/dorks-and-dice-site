using dorks_and_dice_site.Models.Articles;

namespace dorks_and_dice_site.Services.Articles;

public class ArticleCatalogService : IArticleCatalogService
{
    private static readonly List<ArticleItemViewModel> Articles =
    [
        new()
        {
            Title = "Freeing the Bees: Solving ConsoleVariations' Hidden Web Puzzle",
            Summary = "My third-place solve of ConsoleVariations' Free the Bees puzzle, including the visible clue path, encoded password work, browser-state investigation, and final result.",
            Category = "Technical Investigation",
            Controller = "Articles",
            Action = "FreeingTheBeesConsoleVariationsPuzzle",
            ImageUrl = "~/images/articles/consolevariations-bee/ending.png",
            ImageAltText = "Completed ConsoleVariations Queen's Chamber showing the Free the Bees ending screen",
            ImageWidth = 2041,
            ImageHeight = 1220,
            Listed = false,
            Professional = true,
            Tags =
            [
                "technical-investigation",
                "puzzle",
                "write-up"
            ]
        }
    ];

    public ArticlesIndexViewModel GetIndex(bool professionalOnly)
    {
        var visibleArticles = Articles
            .Where(article => article.Listed)
            .Where(article => !professionalOnly || article.Professional)
            .ToList();

        return new ArticlesIndexViewModel
        {
            Articles = visibleArticles,
            ProfessionalFilterActive = professionalOnly,
            IsProfessionalDomain = professionalOnly,
            ShowSearchFilter = true,
            ShowSearchFilterOnProfessional = true,
            ShowCategoryFilter = true,
            ShowCategoryFilterOnProfessional = true,
            ShowProfessionalFilter = true,
            ShowProfessionalFilterOnProfessional = false,
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
