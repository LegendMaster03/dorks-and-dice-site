namespace dorks_and_dice_site.Models.Editor;

public sealed class EditorIndexViewModel
{
    public bool IsTrustedPreview { get; init; }
    public IReadOnlyList<EditorModeOption> Modes { get; init; } = [];
}

public sealed class EditorModeOption
{
    public required string ModeId { get; init; }
    public required string DisplayName { get; init; }
    public string? EditorHref { get; init; }
}
