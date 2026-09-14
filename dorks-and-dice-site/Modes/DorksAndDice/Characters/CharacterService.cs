using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Characters;

public interface ICharacterService
{
    Task<Character> CreateAsync(Guid ownerUserId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Character>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<Character?> GetAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default);
    Task<CampaignCharacterAssociation> ConnectToCampaignAsync(
        Guid ownerUserId,
        Guid characterId,
        Guid campaignId,
        CancellationToken cancellationToken = default);
    Task DisconnectFromCampaignAsync(
        Guid ownerUserId,
        Guid characterId,
        Guid campaignId,
        CancellationToken cancellationToken = default);
}

public sealed class CharacterService(
    DorksAndDiceDbContext dbContext,
    TimeProvider timeProvider) : ICharacterService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Character> CreateAsync(
        Guid ownerUserId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Name = NormalizeName(name),
            Status = CharacterStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Characters.Add(character);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return character;
    }

    public async Task<IReadOnlyList<Character>> GetForOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Characters
            .AsNoTracking()
            .Where(character => character.OwnerUserId == ownerUserId
                && character.Status == CharacterStatus.Active)
            .Include(character => character.CampaignAssociations.Where(association =>
                association.Status == CampaignCharacterAssociationStatus.Active))
                .ThenInclude(association => association.Campaign)
            .OrderBy(character => character.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Character?> GetAsync(
        Guid ownerUserId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId
                && character.OwnerUserId == ownerUserId)
            .Include(character => character.CampaignAssociations)
                .ThenInclude(association => association.Campaign)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CampaignCharacterAssociation> ConnectToCampaignAsync(
        Guid ownerUserId,
        Guid characterId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        if (character.Status != CharacterStatus.Active)
        {
            throw new CampaignDomainException("Archived characters can not be connected to a campaign.");
        }

        var hasPlayerMembership = await _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.UserId == ownerUserId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Campaign.Status == CampaignStatus.Active
                && membership.Roles.Any(role => role.Role == CampaignRoles.Player),
            cancellationToken);
        if (!hasPlayerMembership)
        {
            throw new CampaignDomainException("The character owner must be an active player in the campaign.");
        }

        var activeAssociation = await _dbContext.CampaignCharacters
            .SingleOrDefaultAsync(
                association => association.CharacterId == characterId
                    && association.Status == CampaignCharacterAssociationStatus.Active,
                cancellationToken);
        if (activeAssociation is not null)
        {
            if (activeAssociation.CampaignId == campaignId)
            {
                return activeAssociation;
            }

            throw new CampaignDomainException(
                "A character can only have one active campaign connection at a time.");
        }

        var now = _timeProvider.GetUtcNow();
        var association = new CampaignCharacterAssociation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            CharacterId = characterId,
            Status = CampaignCharacterAssociationStatus.Active,
            ConnectedAt = now
        };
        character.UpdatedAt = now;

        _dbContext.CampaignCharacters.Add(association);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return association;
    }

    public async Task DisconnectFromCampaignAsync(
        Guid ownerUserId,
        Guid characterId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        var association = await _dbContext.CampaignCharacters.SingleOrDefaultAsync(
            item => item.CharacterId == characterId
                && item.CampaignId == campaignId
                && item.Status == CampaignCharacterAssociationStatus.Active,
            cancellationToken);
        if (association is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        association.Status = CampaignCharacterAssociationStatus.Ended;
        association.EndedAt = now;
        association.EndedByUserId = ownerUserId;
        association.EndReason = "Disconnected by character owner";
        character.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Character> GetOwnedCharacterForUpdateAsync(
        Guid ownerUserId,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Characters.SingleOrDefaultAsync(
            character => character.Id == characterId && character.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new CampaignDomainException("Character does not exist or is not owned by this account.");
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 160)
        {
            throw new CampaignDomainException("Character names can not exceed 160 characters.");
        }

        return normalized;
    }
}
