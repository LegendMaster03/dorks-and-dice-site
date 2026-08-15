namespace dorks_and_dice_site.Models.Content;

public sealed class ContentAssetInfo
{
    public string AssetKey { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public long Length { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string Url { get; init; } = string.Empty;
    public string MarkdownReference { get; init; } = string.Empty;
    public string? Relationship { get; init; }
    public string SourceKey { get; init; } = string.Empty;
    public bool IsAttached => Relationship is not null;
}

public sealed class ContentAssetFile
{
    public string FileName { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public byte[] Data { get; init; } = [];
}

public sealed class ContentAssetAuthoringViewModel
{
    public string SourceKey { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public List<ContentAssetInfo> Assets { get; init; } = [];
    public List<ContentAssetInfo> AvailableAssets { get; init; } = [];
    public string SearchQuery { get; init; } = string.Empty;
}

public sealed class ContentAssetLibraryViewModel
{
    public string SourceKey { get; init; } = string.Empty;
    public List<ContentAuthoringSourceOption> Sources { get; init; } = [];
    public List<ContentAssetInfo> Assets { get; init; } = [];
}
