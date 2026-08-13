namespace dorks_and_dice_site.Models.Content;

public sealed class ContentAuthoringDocument
{
    public bool IsNew { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long ExpectedRevisionId { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public string VisibleModesText { get; set; } = string.Empty;
    public string BodyFormat { get; set; } = "markdown";
    public string Body { get; set; } = string.Empty;
}

public sealed class ContentAuthoringEditViewModel
{
    public ContentAuthoringDocument Document { get; set; } = new();
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
}
