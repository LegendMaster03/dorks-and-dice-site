namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public enum CampaignInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3
}

public sealed class CampaignInvitation
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ParticipantId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public CampaignInvitationStatus Status { get; set; } = CampaignInvitationStatus.Pending;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public CampaignParticipant? Participant { get; set; }
}

public sealed record CampaignInvitationGrant(CampaignInvitation Invitation, string Token);

public sealed record CampaignInvitationPreview(
    Guid InvitationId,
    Guid CampaignId,
    string CampaignName,
    IReadOnlyList<string> Roles,
    string? ParticipantName,
    DateTimeOffset ExpiresAt);
