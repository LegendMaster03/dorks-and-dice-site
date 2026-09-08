using dorks_and_dice_site.Models.Campaigns;

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
    public IReadOnlyList<CampaignAccessSummary> Campaigns { get; init; } = [];
}
