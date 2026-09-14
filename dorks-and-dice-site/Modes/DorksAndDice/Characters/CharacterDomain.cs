using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

namespace dorks_and_dice_site.Modes.DorksAndDice.Characters;

public enum CharacterStatus
{
    Active = 0,
    Archived = 1
}

public enum CampaignCharacterAssociationStatus
{
    Active = 0,
    Ended = 1
}

/// <summary>
/// Host-owned character identity. Detailed sheet data belongs to the character-sheet workflow,
/// while ownership and campaign association remain part of the Dorks & Dice domain backbone.
/// </summary>
public sealed class Character
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CharacterStatus Status { get; set; } = CharacterStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CampaignCharacterAssociation> CampaignAssociations { get; set; } = new List<CampaignCharacterAssociation>();
}

/// <summary>
/// Connects an account-owned character to the campaign whose rules/context currently apply.
/// Ending this relationship never transfers or deletes the character.
/// </summary>
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
