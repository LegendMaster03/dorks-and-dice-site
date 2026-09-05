namespace dorks_and_dice_site.Models.Content;

public sealed class ContentAuthoringDocument
{
    public bool IsNew { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long ExpectedRevisionId { get; set; }
    public bool IsListed { get; set; } = true;
    public string MetadataJson { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;

    // Legacy/free-text compatibility input. The checkbox collection below is the normal editor path,
    // so this value is legitimately absent on a submitted form and must not be implicitly required
    // by ASP.NET Core's non-nullable reference-type model validation.
    public string? VisibleModesText { get; set; }

    public List<string> VisibleModesSelection { get; set; } = [];
    public string BodyFormat { get; set; } = "markdown";
    public string Body { get; set; } = string.Empty;
}

public sealed class ContentAuthoringEditViewModel
{
    public ContentAuthoringDocument Document { get; set; } = new();
    public List<ContentAuthoringSourceOption> Sources { get; set; } = [];
    public List<ContentAuthoringModeOption> Modes { get; set; } = [];
    public string? RenderedPreviewHtml { get; set; }
    public List<ContentRevisionSummary> History { get; set; } = [];
}

public sealed class ContentRevisionSummary
{
    public long RevisionId { get; init; }
    public long? ParentRevisionId { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public sealed class ContentAuthoringIndexViewModel
{
    public List<ContentItem> Items { get; init; } = [];
    public string SelectedSourceKey { get; init; } = string.Empty;
    public string AuthoringSourceKey { get; init; } = string.Empty;
    public bool IsAuthoringWorkspace => string.Equals(
        SelectedSourceKey, AuthoringSourceKey, StringComparison.OrdinalIgnoreCase);
    public List<ContentAuthoringSourceOption> Sources { get; init; } = [];
    public List<ContentAuthoringSourceOption> MoveTargets { get; init; } = [];
}

public sealed class ContentAuthoringSourceOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ContentAuthoringModeOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
