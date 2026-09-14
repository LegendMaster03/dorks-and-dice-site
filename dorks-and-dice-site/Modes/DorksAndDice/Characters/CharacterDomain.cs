using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

namespace dorks_and_dice_site.Modes.DorksAndDice.Characters;

public enum CharacterStatus { Active = 0, Archived = 1 }
public enum CampaignCharacterAssociationStatus { Active = 0, Ended = 1 }

public sealed class Character
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CharacterStatus Status { get; set; } = CharacterStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public ICollection<CampaignCharacterAssociation> CampaignAssociations { get; set; } = new List<CampaignCharacterAssociation>();
}

public sealed class CampaignCharacterAssociation
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid CharacterId { get; set; }
    public CampaignCharacterAssociationStatus Status { get; set; } = CampaignCharacterAssociationStatus.Active;
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedByUserId { get; set; }
    public string? EndReason { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Character Character { get; set; } = null!;
}
