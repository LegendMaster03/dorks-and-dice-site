using System.Text;
using System.Text.Json;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Resume;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Resume;
using Microsoft.Data.Sqlite;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  ResumeTxtGenerator <mvc-project-directory>");
    Console.Error.WriteLine("  ResumeTxtGenerator --validate <mvc-project-directory>");
    return 1;
}

var validateOnly = args.Length == 2 && string.Equals(args[0], "--validate", StringComparison.OrdinalIgnoreCase);
var projectDir = validateOnly ? args[1] : args[0];
if (string.IsNullOrWhiteSpace(projectDir))
{
    Console.Error.WriteLine("Project directory is required.");
    return 1;
}

try
{
    var model = ResumePageContentBuilder.Build(projectDir);
    var contentItems = LoadContentItems(projectDir);
    model.ExperienceItems = contentItems.Where(item => item.HasTag(ContentTags.Experience)).ToList();
    model.ProjectItems = contentItems.Where(item => item.HasTag(ContentTags.Project)).ToList();

    if (model.ExperienceItems.Count == 0 || model.ProjectItems.Count == 0)
    {
        throw new InvalidOperationException("Unified content database must contain both experience and project items.");
    }

    if (validateOnly)
    {
        Console.WriteLine($"Validated: {Path.Combine(projectDir, "Content", "Resume", "resume.json")}");
        Console.WriteLine($"Validated: {Path.Combine(projectDir, "Content", "content.db")}");
        return 0;
    }

    var outputPath = Path.Combine(projectDir, "wwwroot", "site-modes", "professional", "files", "kyle-resume.txt");
    var text = ConvertModelToPlainText(model);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, text + "\n", new UTF8Encoding(false));

    Console.WriteLine($"Generated: {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Resume content processing failed: {ex.Message}");
    return 1;
}

static List<ContentItem> LoadContentItems(string projectDir)
{
    var databasePath = Path.Combine(projectDir, "Content", "content.db");
    if (!File.Exists(databasePath))
    {
        throw new FileNotFoundException($"Content database was not found at '{databasePath}'.", databasePath);
    }

    using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
    connection.Open();

    var pageRows = new List<(long PageId, string ContentKey, string Slug, long RevisionId, string MetadataJson)>();
    using (var command = connection.CreateCommand())
    {
        command.CommandText = """
            SELECT p.page_id, p.page_key, p.page_slug, r.revision_id, r.revision_metadata_json
            FROM content_page p
            JOIN content_revision r ON r.revision_id = p.page_current_revision_id
            ORDER BY p.page_id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            pageRows.Add((
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4)));
        }
    }

    var items = new List<ContentItem>();
    foreach (var row in pageRows)
    {
        var item = JsonSerializer.Deserialize<ContentItem>(
            row.MetadataJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Could not read metadata for content page {row.PageId}.");

        item.Id = row.ContentKey;
        item.Slug = row.Slug;
        item.RevisionId = row.RevisionId;
        item.Tags = LoadStrings(connection, "content_revision_tag", "tag", row.RevisionId);
        item.VisibleInModes = LoadStrings(connection, "content_revision_mode", "site_mode", row.RevisionId)
            .Select(mode => Enum.Parse<SiteMode>(mode, ignoreCase: true))
            .ToList();
        items.Add(item);
    }

    return items;
}

static List<string> LoadStrings(SqliteConnection connection, string table, string valueColumn, long revisionId)
{
    using var command = connection.CreateCommand();
    command.CommandText = $"SELECT {valueColumn} FROM {table} WHERE revision_id = $revisionId ORDER BY {valueColumn};";
    command.Parameters.AddWithValue("$revisionId", revisionId);

    var values = new List<string>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        values.Add(reader.GetString(0));
    }
    return values;
}

static string ConvertModelToPlainText(ResumeViewModel model)
{
    var lines = new List<string>
    {
        model.Header.FullName,
        model.Header.Headline,
        model.Header.Location,
        $"Download PDF: {model.Header.ResumePdfFileName} ({model.Header.ResumePdfUrl})",
        $"Last updated: {model.Header.LastUpdatedText}",
        "Contact"
    };

    lines.AddRange(model.ContactLinks.Select(link => $"{link.Label} ({link.Href})"));

    lines.Add("Education");
    foreach (var educationEntry in model.EducationEntries)
    {
        lines.Add(educationEntry.Institution);
        lines.AddRange(educationEntry.Lines.Select(line => line.Text));
    }

    lines.Add("Honors & Awards");
    foreach (var awardEntry in model.AwardEntries)
    {
        lines.Add(awardEntry.Title);
        if (!string.IsNullOrWhiteSpace(awardEntry.MetaText))
        {
            lines.Add(awardEntry.MetaText);
        }

        if (!string.IsNullOrWhiteSpace(awardEntry.Summary))
        {
            lines.Add(awardEntry.Summary);
        }

        lines.AddRange(awardEntry.Highlights.Select(highlight => $"- {highlight}"));

        if (!string.IsNullOrWhiteSpace(awardEntry.AdditionalDescription))
        {
            lines.Add(awardEntry.AdditionalDescription);
        }
    }

    lines.Add("Skills");
    foreach (var skillCategory in model.SkillCategories)
    {
        lines.Add(skillCategory.Name);
        lines.Add(skillCategory.Description);
    }

    lines.Add("Experience");
    foreach (var experienceItem in model.ExperienceItems)
    {
        lines.Add(experienceItem.GetTitle(ContentTags.Experience));
        lines.Add(experienceItem.GetDateText(ContentTags.Experience) ?? string.Empty);
        lines.AddRange(experienceItem.GetHighlights(ContentTags.Experience).Select(highlight => $"- {highlight}"));
    }

    lines.Add("Projects");
    foreach (var projectItem in model.ProjectItems)
    {
        lines.Add(projectItem.GetTitle(ContentTags.Project));
        lines.Add(projectItem.GetSummary(ContentTags.Project));
    }

    lines.Add("Leadership Experience");
    foreach (var leadershipEntry in model.LeadershipEntries)
    {
        lines.Add(leadershipEntry.Title);
        lines.Add(leadershipEntry.DateRange);
        if (!string.IsNullOrWhiteSpace(leadershipEntry.RelatedProjectLabel))
        {
            lines.Add($"Related project: {leadershipEntry.RelatedProjectLabel}");
        }

        lines.AddRange(leadershipEntry.Highlights.Select(highlight => $"- {highlight}"));
    }

    return string.Join(
        "\n",
        lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
}
