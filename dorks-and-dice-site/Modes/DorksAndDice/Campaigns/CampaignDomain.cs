using dorks_and_dice_site.Modes.DorksAndDice.Characters;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public enum CampaignStatus { Active = 0, Archived = 1 }
public enum CampaignMembershipStatus { Active = 0, Left = 1, Removed = 2 }
public enum CampaignParticipantStatus { Active = 0, Former = 1 }

public static class CampaignRoles
{
    public const string Dm = "dm";
    public const string Player = "player";
    private static readonly HashSet<string> KnownRoles = new(StringComparer.OrdinalIgnoreCase) { Dm, Player };

    public static string Normalize(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        var normalized = role.Trim().ToLowerInvariant();
        if (!KnownRoles.Contains(normalized)) throw new CampaignDomainException($"Campaign role '{role}' is not supported.");
        return normalized;
    }

    public static IReadOnlyCollection<string> NormalizeMany(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var normalized = roles.Select(Normalize).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length == 0) throw new CampaignDomainException("An active campaign membership must have at least one role.");
        return normalized;
    }
}

public sealed class Campaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }
    public ICollection<CampaignMembership> Memberships { get; set; } = new List<CampaignMembership>();
    public ICollection<CampaignParticipant> Participants { get; set; } = new List<CampaignParticipant>();
    public ICollection<CampaignCharacterAssociation> CharacterAssociations { get; set; } = new List<CampaignCharacterAssociation>();
}

public sealed class CampaignMembership
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
    public CampaignMembershipStatus Status { get; set; } = CampaignMembershipStatus.Active;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedByUserId { get; set; }
    public string? EndReason { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public ICollection<CampaignMembershipRole> Roles { get; set; } = new List<CampaignMembershipRole>();
}

public sealed class CampaignMembershipRole
{
    public Guid Id { get; set; }
    public Guid CampaignMembershipId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; }
    public Guid GrantedByUserId { get; set; }
    public CampaignMembership CampaignMembership { get; set; } = null!;
}

public sealed class CampaignParticipant
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public CampaignParticipantStatus Status { get; set; } = CampaignParticipantStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedByUserId { get; set; }
    public string? EndReason { get; set; }
    public Campaign Campaign { get; set; } = null!;
}

public sealed class CampaignDomainException : InvalidOperationException
{
    public CampaignDomainException(string message) : base(message) { }
    public CampaignDomainException(string message, Exception innerException) : base(message, innerException) { }
}
