using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentCollectionPageComponentDefinition : IContentPageComponentDefinition
{
    private static readonly Regex KeyPattern = new(
        "^[a-z0-9][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly IReadOnlySet<string> AllowedParameters = new HashSet<string>(
        ["context", "presentation", "order", "featured-first"],
        StringComparer.OrdinalIgnoreCase);

    public string Name => "content-collection";
    public string ViewComponentName => "ContentCollection";

    public void Validate(IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var key in parameters.Keys)
        {
            if (!AllowedParameters.Contains(key))
            {
                throw new InvalidOperationException(
                    $"Content page component '{Name}' does not support parameter '{key}'.");
            }
        }

        var contextTag = GetRequired(parameters, "context");
        if (!ContentTags.ContextTags.Contains(contextTag)
            || string.Equals(contextTag, ContentTags.Homepage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Content page component '{Name}' can list project, experience, or article content, not '{contextTag}'.");
        }

        var presentation = GetRequired(parameters, "presentation");
        ValidateKey("presentation", presentation);

        if (parameters.TryGetValue("order", out var order) && !string.IsNullOrWhiteSpace(order))
        {
            var slugs = order.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (slugs.Length > 100)
            {
                throw new InvalidOperationException("A content collection may specify at most 100 ordered slugs.");
            }

            foreach (var slug in slugs)
            {
                ValidateKey("order slug", slug);
            }
        }

        if (parameters.TryGetValue("featured-first", out var featuredFirst)
            && !string.IsNullOrWhiteSpace(featuredFirst)
            && !bool.TryParse(featuredFirst, out _))
        {
            throw new InvalidOperationException(
                $"Content page component '{Name}' parameter 'featured-first' must be true or false.");
        }
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Content page component 'content-collection' requires parameter '{name}'.");
        }

        return value;
    }

    private static void ValidateKey(string fieldName, string value)
    {
        if (value.Length > ContentInputPolicy.MaxKeyLength || !KeyPattern.IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Content collection {fieldName} '{value}' is invalid.");
        }
    }
}

public sealed class ContentCollectionViewComponent : ViewComponent
{
    private readonly IContentCatalogService _catalog;

    public ContentCollectionViewComponent(IContentCatalogService catalog)
    {
        _catalog = catalog;
    }

    public async Task<IViewComponentResult> InvokeAsync(ContentPageComponentInvocation request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contextTag = request.GetRequiredParameter("context");
        var presentationKey = request.GetRequiredParameter("presentation");
        var modeContext = HttpContext.GetSiteModeContext();
        var items = await _catalog.GetByContextAsync(
            contextTag,
            modeContext,
            includeUnlisted: false,
            HttpContext.RequestAborted);

        var ordered = ApplyOrdering(items, contextTag, request);

        // Presentation keys are validated as lowercase/hyphen identifiers by the component
        // definition, so authored data can only select an installed view under this directory
        // and can not choose an arbitrary Razor path.
        return View($"~/Views/ContentCollections/{presentationKey}.cshtml", ordered);
    }

    private static IReadOnlyList<ContentItem> ApplyOrdering(
        IReadOnlyList<ContentItem> items,
        string contextTag,
        ContentPageComponentInvocation request)
    {
        var orderText = request.GetOptionalParameter("order");
        var explicitOrder = (orderText ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((slug, index) => new { slug, index })
            .ToDictionary(entry => entry.slug, entry => entry.index, StringComparer.OrdinalIgnoreCase);

        IEnumerable<ContentItem> ordered = items
            .Select((item, sourceIndex) => new { item, sourceIndex })
            .OrderBy(entry => explicitOrder.TryGetValue(entry.item.Slug, out var index) ? index : int.MaxValue)
            .ThenBy(entry => entry.sourceIndex)
            .Select(entry => entry.item);

        if (bool.TryParse(request.GetOptionalParameter("featured-first"), out var featuredFirst)
            && featuredFirst)
        {
            ordered = ordered
                .Select((item, index) => new { item, index })
                .OrderByDescending(entry => entry.item.IsFeatured(contextTag))
                .ThenBy(entry => entry.index)
                .Select(entry => entry.item);
        }

        return ordered.ToList();
    }
}
