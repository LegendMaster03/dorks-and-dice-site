using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Models.Operator;

public sealed record OperatorMeResponse(
    Guid UserId,
    string DisplayName,
    string AccountKind,
    IReadOnlyList<string> GlobalRoles);

public sealed record OperatorToolSummary(
    string Slug,
    string DisplayName,
    int OperatorContractVersion,
    string ManifestPath);

public sealed record OperatorCapabilitiesResponse(
    IReadOnlyList<OperatorCapabilityDescriptor> Site,
    IReadOnlyList<OperatorToolSummary> Tools);

public sealed record OperatorContentListItem(
    string SourceKey,
    string Id,
    string Slug,
    string Title,
    string Summary,
    long RevisionId,
    bool IsListed,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> VisibleModes);

public sealed record OperatorContentRevision(
    long RevisionId,
    long? ParentRevisionId,
    DateTime CreatedUtc);

public sealed record OperatorContentDocumentResponse(
    string SourceKey,
    string Id,
    string Slug,
    long RevisionId,
    bool IsListed,
    string MetadataJson,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> VisibleModes,
    string BodyFormat,
    string Body,
    IReadOnlyList<OperatorContentRevision> History);

public sealed class OperatorContentWriteRequest
{
    public string Id { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public long ExpectedRevisionId { get; init; }
    public bool IsListed { get; init; } = true;
    public string MetadataJson { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> VisibleModes { get; init; } = [];
    public string BodyFormat { get; init; } = "markdown";
    public string Body { get; init; } = string.Empty;
}

public sealed class OperatorContentPreviewRequest
{
    public string BodyFormat { get; init; } = "markdown";
    public string Body { get; init; } = string.Empty;
}

public sealed record OperatorContentPreviewResponse(string Html);

public sealed class OperatorMediaUploadRequest
{
    public string FileName { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public string Base64Data { get; init; } = string.Empty;
}
