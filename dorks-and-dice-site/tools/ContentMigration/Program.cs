using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ContentMigration <mvc-project-directory>");
    return 1;
}

var projectDir = Path.GetFullPath(args[0]);
var resumePath = Path.Combine(projectDir, "Content", "Resume", "resume.json");
var outputPath = Path.Combine(projectDir, "Content", "content.db");

if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume source was not found at '{resumePath}'.");
    return 1;
}

try
{
    var items = BuildItems(projectDir, resumePath);
    WriteDatabase(outputPath, items);
    Console.WriteLine($"Generated {items.Count} content pages at {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Content migration failed: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static List<SeedItem> BuildItems(string projectDir, string resumePath)
{
    using var resumeDocument = JsonDocument.Parse(File.ReadAllText(resumePath));
    var root = resumeDocument.RootElement;
    var itemsByAction = new Dictionary<string, SeedItem>(StringComparer.OrdinalIgnoreCase);

    var idsByAction = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["XnGine"] = "xngine-framework",
        ["PythonFinanceAnalytics"] = "python-finance-analytics",
        ["PersonalMultiModeWebsite"] = "personal-multi-mode-website",
        ["SeniorProject"] = "safe-future-barcode-for-good",
        ["DirectedIndependentStudy"] = "directed-independent-study",
        ["Skyblivion"] = "skyblivion-interiors",
        ["Skywind"] = "skywind-interiors",
        ["SimLabExpo"] = "weed-wacker",
        ["DndTools"] = "dnd-campaign-systems",
        ["ExperienceCaspEnterprises"] = "casp-enterprises-it-manager",
        ["ExperienceTechnologyServices"] = "independent-technology-consultant",
        ["ExperienceCyberSecurityTeam"] = "unf-cyber-security-team",
        ["ExperienceSimLab"] = "florida-poly-sim-lab",
        ["ExperienceWiredWorks"] = "wired-works-installer"
    };

    var actionAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ExperienceSkyblivion"] = "Skyblivion"
    };

    foreach (var project in root.GetProperty("projectItems").EnumerateArray())
    {
        var action = project.GetProperty("action").GetString()
            ?? throw new InvalidOperationException("Project action is required.");
        var item = new SeedItem
        {
            Id = idsByAction[action],
            Slug = action.ToLowerInvariant(),
            Title = project.GetProperty("title").GetString() ?? string.Empty,
            Subtitle = GetOptionalString(project, "subtitle"),
            Summary = project.GetProperty("summary").GetString() ?? string.Empty,
            Category = GetOptionalString(project, "category"),
            RepositoryUrl = GetOptionalString(project, "repositoryUrl"),
            Featured = GetOptionalBoolean(project, "featured"),
            DetailSourcePath = Path.Combine(projectDir, "Views", "SiteModes", "Professional", "Resume", "Projects", action + ".cshtml")
        };

        item.Tags.Add("project");
        item.Tags.UnionWith(GetStringArray(project, "tags"));
        item.VisibleInModes.Add("Professional");
        item.Presentations["project"] = new SeedPresentation
        {
            Title = item.Title,
            Subtitle = item.Subtitle,
            Summary = item.Summary,
            Category = item.Category,
            LinkText = "Open Project",
            Featured = item.Featured
        };
        itemsByAction[action] = item;
    }

    foreach (var experience in root.GetProperty("experienceItems").EnumerateArray())
    {
        var sourceAction = experience.GetProperty("detailsAction").GetString();
        if (string.IsNullOrWhiteSpace(sourceAction))
        {
            continue;
        }

        var action = actionAliases.TryGetValue(sourceAction, out var alias) ? alias : sourceAction;
        var highlights = GetStringArray(experience, "highlights").ToList();
        if (!itemsByAction.TryGetValue(action, out var item))
        {
            item = new SeedItem
            {
                Id = idsByAction[action],
                Slug = action.ToLowerInvariant(),
                Title = experience.GetProperty("title").GetString() ?? string.Empty,
                Summary = highlights.Count > 0 ? string.Join(" ", highlights) : experience.GetProperty("title").GetString() ?? string.Empty,
                Category = "professional",
                DetailSourcePath = Path.Combine(projectDir, "Views", "SiteModes", "Professional", "Resume", "Experience", sourceAction + ".cshtml")
            };
            item.VisibleInModes.Add("Professional");
            itemsByAction[action] = item;
        }

        item.Tags.Add("experience");
        item.Tags.UnionWith(GetStringArray(experience, "tags"));
        item.Presentations["experience"] = new SeedPresentation
        {
            Title = experience.GetProperty("title").GetString(),
            DateText = experience.GetProperty("dateRange").GetString(),
            Highlights = highlights,
            LinkText = "Detailed experience view",
            Featured = GetOptionalBoolean(experience, "featured")
        };
    }

    foreach (var item in itemsByAction.Values)
    {
        EnrichFromDetailSource(item);
    }

    var article = new SeedItem
    {
        Id = "consolevariations-free-the-bees",
        Slug = "freeing-the-bees-consolevariations-puzzle",
        Title = "Freeing the Bees: Solving ConsoleVariations' Hidden Web Puzzle",
        Summary = "My third-place solve of ConsoleVariations' Free the Bees puzzle, including the visible clue path, encoded password work, browser-state investigation, and final result.",
        Category = "Technical Investigation",
        DateText = "August 12, 2026",
        MetaTitle = "Freeing the Bees: Solving ConsoleVariations' Hidden Web Puzzle",
        MetaDescription = "Kyle Barnett's third-place walkthrough and technical investigation of the ConsoleVariations Free the Bees web puzzle.",
        MetaImage = "/site-modes/professional/images/articles/consolevariations-bee/ending.png",
        ListingImage = new SeedImage
        {
            Url = "/site-modes/professional/images/articles/consolevariations-bee/ending.png",
            AltText = "Completed ConsoleVariations Queen's Chamber showing the Free the Bees ending screen",
            Width = 2041,
            Height = 1220
        },
        DetailSourcePath = Path.Combine(projectDir, "Views", "Articles", "FreeingTheBeesConsoleVariationsPuzzle.cshtml")
    };
    article.Tags.UnionWith(["article", "technical-investigation", "puzzle", "write-up"]);
    article.VisibleInModes.Add("Professional");
    article.Presentations["article"] = new SeedPresentation
    {
        Title = article.Title,
        Summary = article.Summary,
        DateText = article.DateText,
        Category = article.Category,
        LinkText = "Read Article"
    };
    EnrichFromDetailSource(article);
    article.Header.CssClass = "consolevariations-bee";
    article.Header.InfoItems["Posted"] = "August 12, 2026";

    var result = itemsByAction.Values.Append(article).OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();
    Validate(result);
    return result;
}

static void EnrichFromDetailSource(SeedItem item)
{
    if (string.IsNullOrWhiteSpace(item.DetailSourcePath) || !File.Exists(item.DetailSourcePath))
    {
        throw new FileNotFoundException($"Detail source was not found for '{item.Id}'.", item.DetailSourcePath);
    }

    var source = File.ReadAllText(item.DetailSourcePath);
    var marker = "@await Html.PartialAsync";
    var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
    if (markerIndex < 0)
    {
        throw new InvalidOperationException($"Detail source '{item.DetailSourcePath}' does not contain the shared header marker.");
    }

    var markerLineEnd = source.IndexOf('\n', markerIndex);
    var bodyStart = markerLineEnd >= 0 ? markerLineEnd + 1 : source.Length;
    var prefix = source[..markerIndex];
    var body = source[bodyStart..].Trim();

    var detailTitle = ParseAssignment(prefix, "Title");
    var subtitle = ParseAssignment(prefix, "Subtitle");
    if (!string.IsNullOrWhiteSpace(detailTitle))
    {
        item.Title = detailTitle;
    }
    if (!string.IsNullOrWhiteSpace(subtitle))
    {
        item.Subtitle = subtitle;
    }

    item.Header.MetaLine = ParseAssignment(prefix, "MetaLine");
    item.Header.LogoUrl = ParseUrlAssignment(prefix, "LogoUrl");
    item.Header.LogoAltText = ParseAssignment(prefix, "LogoAltText");
    item.Header.LogoLinkUrl = ParseAssignment(prefix, "LogoLinkUrl");
    item.Header.LogoAriaLabel = ParseAssignment(prefix, "LogoAriaLabel");
    item.Header.InfoItems = ParseDictionary(prefix, "InfoItems");
    item.Header.InfoItemLinks = ParseDictionary(prefix, "InfoItemLinks");

    if (item.Id == "dnd-campaign-systems")
    {
        item.Header.LogoUrl = "/favicon.ico";
    }

    if (item.Id == "personal-multi-mode-website")
    {
        const string dynamicStart = "<h2 class=\"h5 mt-4\">Live Mode Matrix</h2>";
        const string dynamicEnd = "<h2 class=\"h5 mt-4\">Request Flow</h2>";
        var start = body.IndexOf(dynamicStart, StringComparison.Ordinal);
        var end = body.IndexOf(dynamicEnd, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("Could not locate the dynamic architecture section in PersonalMultiModeWebsite.cshtml.");
        }

        body = body[..start] + "{{site-mode-architecture}}\n\n" + body[end..];
    }

    body = body.Replace("~/", "/", StringComparison.Ordinal);
    if (Regex.IsMatch(body, "@(foreach|if|inject|using|\\{)", RegexOptions.CultureInvariant))
    {
        throw new InvalidOperationException($"Unmigrated Razor syntax remains in the body for '{item.Id}'.");
    }

    item.BodyFormat = "markdown";
    item.Body = body;
}

static string? ParseAssignment(string source, string property)
{
    var pattern = "\\b" + Regex.Escape(property) + "\\s*=\\s*\"(?<value>(?:\\\\.|[^\"])*)\"";
    var match = Regex.Match(source, pattern, RegexOptions.CultureInvariant);
    return match.Success ? DecodeCSharpString(match.Groups["value"].Value) : null;
}

static string? ParseUrlAssignment(string source, string property)
{
    var direct = ParseAssignment(source, property);
    if (!string.IsNullOrWhiteSpace(direct))
    {
        return direct.Replace("~/", "/", StringComparison.Ordinal);
    }

    var pattern = "\\b" + Regex.Escape(property) + "\\s*=\\s*Url\\.Content\\(\"(?<value>(?:\\\\.|[^\"])*)\"\\)";
    var match = Regex.Match(source, pattern, RegexOptions.CultureInvariant);
    return match.Success
        ? DecodeCSharpString(match.Groups["value"].Value).Replace("~/", "/", StringComparison.Ordinal)
        : null;
}

static Dictionary<string, string> ParseDictionary(string source, string property)
{
    var start = source.IndexOf(property, StringComparison.Ordinal);
    if (start < 0)
    {
        return [];
    }

    var nextProperty = property == "InfoItems" ? "InfoItemLinks" : "};";
    var end = source.IndexOf(nextProperty, start + property.Length, StringComparison.Ordinal);
    var block = end > start ? source[start..end] : source[start..];
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    const string pattern = "\\[\"(?<key>(?:\\\\.|[^\"])*)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"])*)\"";

    foreach (Match match in Regex.Matches(block, pattern, RegexOptions.CultureInvariant))
    {
        values[DecodeCSharpString(match.Groups["key"].Value)] = DecodeCSharpString(match.Groups["value"].Value);
    }

    return values;
}

static string DecodeCSharpString(string value) => value
    .Replace("\\\"", "\"", StringComparison.Ordinal)
    .Replace("\\\\", "\\", StringComparison.Ordinal);

static string? GetOptionalString(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;

static bool GetOptionalBoolean(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property)
    && property.ValueKind is JsonValueKind.True or JsonValueKind.False
    && property.GetBoolean();

static IEnumerable<string> GetStringArray(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
    {
        yield break;
    }

    foreach (var value in property.EnumerateArray())
    {
        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            yield return value.GetString()!;
        }
    }
}

static void Validate(IReadOnlyList<SeedItem> items)
{
    if (items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
    {
        throw new InvalidOperationException("Content migration produced duplicate stable IDs.");
    }

    if (items.Select(item => item.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
    {
        throw new InvalidOperationException("Content migration produced duplicate slugs.");
    }

    foreach (var item in items)
    {
        if (string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.Slug)
            || string.IsNullOrWhiteSpace(item.Title)
            || string.IsNullOrWhiteSpace(item.Summary)
            || item.Tags.Count == 0
            || item.VisibleInModes.Count == 0
            || string.IsNullOrWhiteSpace(item.Body))
        {
            throw new InvalidOperationException($"Content migration produced an incomplete item '{item.Id}'.");
        }
    }
}

static void WriteDatabase(string outputPath, IReadOnlyList<SeedItem> items)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    if (File.Exists(outputPath))
    {
        File.Delete(outputPath);
    }

    using var connection = new SqliteConnection($"Data Source={outputPath}");
    connection.Open();

    using (var command = connection.CreateCommand())
    {
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE content_page (
                page_id INTEGER NOT NULL CONSTRAINT PK_content_page PRIMARY KEY AUTOINCREMENT,
                page_key TEXT NOT NULL,
                page_slug TEXT NOT NULL,
                page_current_revision_id INTEGER NULL,
                CONSTRAINT FK_content_page_content_revision_page_current_revision_id
                    FOREIGN KEY (page_current_revision_id) REFERENCES content_revision (revision_id) ON DELETE RESTRICT
            );

            CREATE TABLE content_revision (
                revision_id INTEGER NOT NULL CONSTRAINT PK_content_revision PRIMARY KEY AUTOINCREMENT,
                revision_page_id INTEGER NOT NULL,
                revision_parent_id INTEGER NULL,
                revision_created_utc TEXT NOT NULL,
                revision_body_format TEXT NOT NULL,
                revision_metadata_json TEXT NOT NULL,
                revision_body TEXT NOT NULL,
                CONSTRAINT FK_content_revision_content_page_revision_page_id
                    FOREIGN KEY (revision_page_id) REFERENCES content_page (page_id) ON DELETE CASCADE,
                CONSTRAINT FK_content_revision_content_revision_revision_parent_id
                    FOREIGN KEY (revision_parent_id) REFERENCES content_revision (revision_id) ON DELETE RESTRICT
            );

            CREATE TABLE content_revision_tag (
                revision_id INTEGER NOT NULL,
                tag TEXT NOT NULL,
                CONSTRAINT PK_content_revision_tag PRIMARY KEY (revision_id, tag),
                CONSTRAINT FK_content_revision_tag_content_revision_revision_id
                    FOREIGN KEY (revision_id) REFERENCES content_revision (revision_id) ON DELETE CASCADE
            );

            CREATE TABLE content_revision_mode (
                revision_id INTEGER NOT NULL,
                site_mode TEXT NOT NULL,
                CONSTRAINT PK_content_revision_mode PRIMARY KEY (revision_id, site_mode),
                CONSTRAINT FK_content_revision_mode_content_revision_revision_id
                    FOREIGN KEY (revision_id) REFERENCES content_revision (revision_id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IX_content_page_page_key ON content_page (page_key);
            CREATE UNIQUE INDEX IX_content_page_page_slug ON content_page (page_slug);
            CREATE INDEX IX_content_page_page_current_revision_id ON content_page (page_current_revision_id);
            CREATE INDEX IX_content_revision_revision_page_id_revision_created_utc
                ON content_revision (revision_page_id, revision_created_utc);
            CREATE INDEX IX_content_revision_revision_parent_id ON content_revision (revision_parent_id);
            CREATE INDEX IX_content_revision_tag_tag ON content_revision_tag (tag);
            CREATE INDEX IX_content_revision_mode_site_mode ON content_revision_mode (site_mode);
            """;
        command.ExecuteNonQuery();
    }

    using var transaction = connection.BeginTransaction();
    foreach (var item in items)
    {
        var pageId = InsertPage(connection, transaction, item);
        var revisionId = InsertRevision(connection, transaction, pageId, item);
        InsertTags(connection, transaction, revisionId, item.Tags);
        InsertModes(connection, transaction, revisionId, item.VisibleInModes);
        SetCurrentRevision(connection, transaction, pageId, revisionId);
    }
    transaction.Commit();
}

static long InsertPage(SqliteConnection connection, SqliteTransaction transaction, SeedItem item)
{
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "INSERT INTO content_page (page_key, page_slug, page_current_revision_id) VALUES ($key, $slug, NULL); SELECT last_insert_rowid();";
    command.Parameters.AddWithValue("$key", item.Id);
    command.Parameters.AddWithValue("$slug", item.Slug);
    return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Could not create content page."));
}

static long InsertRevision(SqliteConnection connection, SqliteTransaction transaction, long pageId, SeedItem item)
{
    var metadata = JsonSerializer.Serialize(new
    {
        item.Title,
        item.Subtitle,
        item.Summary,
        item.DateText,
        item.Category,
        item.RepositoryUrl,
        item.LinkText,
        item.Featured,
        item.MetaTitle,
        item.MetaDescription,
        item.MetaImage,
        item.ListingImage,
        item.Header,
        Highlights = item.Highlights,
        Presentations = item.Presentations
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO content_revision
            (revision_page_id, revision_parent_id, revision_created_utc, revision_body_format, revision_metadata_json, revision_body)
        VALUES
            ($pageId, NULL, $createdUtc, $bodyFormat, $metadata, $body);
        SELECT last_insert_rowid();
        """;
    command.Parameters.AddWithValue("$pageId", pageId);
    command.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
    command.Parameters.AddWithValue("$bodyFormat", item.BodyFormat);
    command.Parameters.AddWithValue("$metadata", metadata);
    command.Parameters.AddWithValue("$body", item.Body);
    return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Could not create content revision."));
}

static void InsertTags(SqliteConnection connection, SqliteTransaction transaction, long revisionId, IEnumerable<string> tags)
{
    foreach (var tag in tags.Order(StringComparer.OrdinalIgnoreCase))
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO content_revision_tag (revision_id, tag) VALUES ($revisionId, $tag);";
        command.Parameters.AddWithValue("$revisionId", revisionId);
        command.Parameters.AddWithValue("$tag", tag);
        command.ExecuteNonQuery();
    }
}

static void InsertModes(SqliteConnection connection, SqliteTransaction transaction, long revisionId, IEnumerable<string> modes)
{
    foreach (var mode in modes.Order(StringComparer.OrdinalIgnoreCase))
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO content_revision_mode (revision_id, site_mode) VALUES ($revisionId, $mode);";
        command.Parameters.AddWithValue("$revisionId", revisionId);
        command.Parameters.AddWithValue("$mode", mode);
        command.ExecuteNonQuery();
    }
}

static void SetCurrentRevision(SqliteConnection connection, SqliteTransaction transaction, long pageId, long revisionId)
{
    using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "UPDATE content_page SET page_current_revision_id = $revisionId WHERE page_id = $pageId;";
    command.Parameters.AddWithValue("$revisionId", revisionId);
    command.Parameters.AddWithValue("$pageId", pageId);
    command.ExecuteNonQuery();
}

sealed class SeedItem
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DateText { get; set; }
    public string? Category { get; set; }
    public string? RepositoryUrl { get; set; }
    public string LinkText { get; set; } = "Open details";
    public bool Featured { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaImage { get; set; }
    public SeedImage? ListingImage { get; set; }
    public SeedHeader Header { get; set; } = new();
    public List<string> Highlights { get; set; } = [];
    public Dictionary<string, SeedPresentation> Presentations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> VisibleInModes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string BodyFormat { get; set; } = "markdown";
    public string Body { get; set; } = string.Empty;
    public string? DetailSourcePath { get; set; }
}

sealed class SeedPresentation
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Summary { get; set; }
    public string? DateText { get; set; }
    public string? Category { get; set; }
    public string? LinkText { get; set; }
    public bool? Featured { get; set; }
    public List<string>? Highlights { get; set; }
}

sealed class SeedImage
{
    public string Url { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

sealed class SeedHeader
{
    public string? MetaLine { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoAltText { get; set; }
    public string? LogoLinkUrl { get; set; }
    public string? LogoAriaLabel { get; set; }
    public string? CssClass { get; set; }
    public Dictionary<string, string> InfoItems { get; set; } = [];
    public Dictionary<string, string> InfoItemLinks { get; set; } = [];
}
