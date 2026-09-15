namespace dorks_and_dice_site.Models.Tools;

public sealed class ToolHostApiSession
{
    public int ContractVersion { get; init; } = 1;
    public required string ToolSlug { get; init; }
    public required string SiteMode { get; init; }
    public required ToolHostUserContext User { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];
}

/// <summary>
/// Campaign authorization summary exposed through the Tool Host contract. This projection is
/// independent of Dorks & Dice campaign persistence and exists only as a Tool integration DTO.
/// Contract version 1 carries one campaign role per entry.
/// </summary>
public sealed class ToolHostCampaignAccessSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}

/// <summary>
/// Authoritative identity/authorization context redeemed by a Tool backend from a short-lived
/// host-issued authentication ticket. Source-content grants intentionally do not live here;
/// those remain owned by the Tool that stores the protected content.
/// </summary>
public sealed class ToolHostAuthenticationContext
{
    public int ContractVersion { get; init; } = 1;
    public required string ToolSlug { get; init; }
    public required string SiteMode { get; init; }
    public required ToolHostUserContext User { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];
    public IReadOnlyList<ToolHostCampaignAccessSummary> Campaigns { get; init; } = [];
}
