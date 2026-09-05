namespace dorks_and_dice_site.Models.Tools;

public sealed class ToolHostContext
{
    public int ContractVersion { get; init; } = 1;
    public required string ToolSlug { get; init; }
    public required string SiteMode { get; init; }
    public ToolHostUserContext? User { get; init; }
}

public sealed class ToolHostUserContext
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
}
