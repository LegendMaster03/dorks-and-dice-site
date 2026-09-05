namespace dorks_and_dice_site.Models.Tools;

public sealed class ToolHostApiSession
{
    public int ContractVersion { get; init; } = 1;
    public required string ToolSlug { get; init; }
    public required string SiteMode { get; init; }
    public required ToolHostUserContext User { get; init; }
}
