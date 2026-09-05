namespace dorks_and_dice_site.Models.Campaigns;

public static class CampaignRoles
{
    public const string Dm = "DM";
    public const string Player = "Player";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Dm, Player],
        StringComparer.Ordinal);
}

public sealed class CampaignRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CampaignMembershipRecord
{
    public Guid CampaignId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = CampaignRoles.Player;
}

public sealed class CampaignAccessDocument
{
    public List<CampaignRecord> Campaigns { get; set; } = [];
    public List<CampaignMembershipRecord> Memberships { get; set; } = [];
}

public sealed class CampaignAccessSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}
