using System.Text.Json;
using System.Text.Json.Serialization;
using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public static class ContentFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static IReadOnlyList<ContentItem> LoadAll(string contentRootPath)
    {
        var itemsRoot = Path.Combine(contentRootPath, "Content", "Items");
        if (!Directory.Exists(itemsRoot))
        {
            throw new DirectoryNotFoundException($"Content item directory was not found at '{itemsRoot}'.");
        }

        var items = new List<ContentItem>();
        foreach (var metadataPath in Directory.EnumerateFiles(itemsRoot, "item.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(metadataPath);
            var item = JsonSerializer.Deserialize<ContentItem>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize content metadata from '{metadataPath}'.");

            Normalize(item);
            var bodyPath = Path.Combine(Path.GetDirectoryName(metadataPath)!, "body.md");
            if (!File.Exists(bodyPath))
            {
                throw new FileNotFoundException($"Content body was not found for '{item.Id}' at '{bodyPath}'.", bodyPath);
            }

            item.BodyMarkdown = File.ReadAllText(bodyPath);
            ValidateOrThrow(item, metadataPath);
            items.Add(item);
        }

        ValidateUniqueness(items, itemsRoot);
        return items;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void Normalize(ContentItem item)
    {
        item.Tags ??= [];
        item.VisibleInModes ??= [];
        item.Highlights ??= [];
        item.Presentations ??= new Dictionary<string, ContentPresentation>(StringComparer.OrdinalIgnoreCase);
        item.Header ??= new ContentDetailHeader();
        item.Header.InfoItems ??= [];
        item.Header.InfoItemLinks ??= [];
    }

    private static void ValidateOrThrow(ContentItem item, string sourcePath)
    {
        var errors = new List<string>();

        if (item.SchemaVersion != 1)
        {
            errors.Add($"schemaVersion '{item.SchemaVersion}' is not supported.");
        }

        Require(item.Id, "id", errors);
        Require(item.Slug, "slug", errors);
        Require(item.Title, "title", errors);
        Require(item.Summary, "summary", errors);

        if (!item.Tags.Any(ContentTags.IsContext))
        {
            errors.Add("tags must contain at least one content context tag: project, experience, or article.");
        }

        if (item.VisibleInModes.Count == 0)
        {
            errors.Add("visibleInModes must contain at least one site mode.");
        }

        foreach (var contextTag in item.Presentations.Keys)
        {
            if (!ContentTags.IsContext(contextTag))
            {
                errors.Add($"presentation key '{contextTag}' is not a supported content context tag.");
            }

            if (!item.HasTag(contextTag))
            {
                errors.Add($"presentation key '{contextTag}' requires the same context tag in tags.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Content validation failed for '{sourcePath}':{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}");
        }
    }

    private static void ValidateUniqueness(IReadOnlyList<ContentItem> items, string sourcePath)
    {
        var duplicateIds = items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        var duplicateSlugs = items
            .GroupBy(item => item.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count == 0 && duplicateSlugs.Count == 0)
        {
            return;
        }

        var errors = new List<string>();
        if (duplicateIds.Count > 0)
        {
            errors.Add($"duplicate ids: {string.Join(", ", duplicateIds)}");
        }

        if (duplicateSlugs.Count > 0)
        {
            errors.Add($"duplicate slugs: {string.Join(", ", duplicateSlugs)}");
        }

        throw new InvalidOperationException($"Content uniqueness validation failed under '{sourcePath}': {string.Join("; ", errors)}.");
    }

    private static void Require(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
