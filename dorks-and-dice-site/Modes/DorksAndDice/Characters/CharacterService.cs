using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Characters;

public interface ICharacterService
{
    Task<Character> CreateAsync(Guid ownerUserId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Character>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Character>> GetForOwnerIncludingArchivedAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<Character?> GetAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default);
    Task RenameAsync(Guid ownerUserId, Guid characterId, string name, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default);
    Task<CampaignCharacterAssociation> ConnectToCampaignAsync(Guid ownerUserId, Guid characterId, Guid campaignId, CancellationToken cancellationToken = default);
    Task DisconnectFromCampaignAsync(Guid ownerUserId, Guid characterId, Guid campaignId, CancellationToken cancellationToken = default);
}

public sealed class CharacterService(DorksAndDiceDbContext dbContext, ICampaignAccessService campaignAccess, TimeProvider timeProvider) : ICharacterService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Character> CreateAsync(Guid ownerUserId, string name, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var character = new Character { Id = Guid.NewGuid(), OwnerUserId = ownerUserId, Name = NormalizeName(name), Status = CharacterStatus.Active, CreatedAt = now, UpdatedAt = now };
        _dbContext.Characters.Add(character);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return character;
    }

    public Task<IReadOnlyList<Character>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
        GetForOwnerInternalAsync(ownerUserId, false, cancellationToken);

    public Task<IReadOnlyList<Character>> GetForOwnerIncludingArchivedAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
        GetForOwnerInternalAsync(ownerUserId, true, cancellationToken);

    private async Task<IReadOnlyList<Character>> GetForOwnerInternalAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        var query = _dbContext.Characters.AsNoTracking().Where(character => character.OwnerUserId == ownerUserId);
        if (!includeArchived) query = query.Where(character => character.Status == CharacterStatus.Active);
        return await query.Include(character => character.CampaignAssociations.Where(association => association.Status == CampaignCharacterAssociationStatus.Active))
            .ThenInclude(association => association.Campaign)
            .OrderBy(character => character.Status).ThenBy(character => character.Name).ToListAsync(cancellationToken);
    }

    public async Task<Character?> GetAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default) =>
        await _dbContext.Characters.AsNoTracking().Where(character => character.Id == characterId && character.OwnerUserId == ownerUserId)
            .Include(character => character.CampaignAssociations).ThenInclude(association => association.Campaign).SingleOrDefaultAsync(cancellationToken);

    public async Task RenameAsync(Guid ownerUserId, Guid characterId, string name, CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        character.Name = NormalizeName(name);
        character.UpdatedAt = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        if (character.Status == CharacterStatus.Archived) return;
        var now = _timeProvider.GetUtcNow();
        character.Status = CharacterStatus.Archived;
        character.ArchivedAt = now;
        character.UpdatedAt = now;
        var associations = await _dbContext.CampaignCharacters.Where(item => item.CharacterId == characterId && item.Status == CampaignCharacterAssociationStatus.Active).ToListAsync(cancellationToken);
        foreach (var association in associations) EndAssociation(association, ownerUserId, "Character archived", now);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        if (character.Status == CharacterStatus.Active) return;
        character.Status = CharacterStatus.Active;
        character.ArchivedAt = null;
        character.UpdatedAt = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CampaignCharacterAssociation> ConnectToCampaignAsync(Guid ownerUserId, Guid characterId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        if (character.Status != CharacterStatus.Active) throw new CampaignDomainException("Archived characters can not be connected to a campaign.");
        if (!await _campaignAccess.HasRoleAsync(ownerUserId, campaignId, CampaignRoles.Player, cancellationToken)) throw new CampaignDomainException("The character owner must be an active player in the campaign.");
        var existing = await _dbContext.CampaignCharacters.SingleOrDefaultAsync(item => item.CharacterId == characterId && item.CampaignId == campaignId && item.Status == CampaignCharacterAssociationStatus.Active, cancellationToken);
        if (existing is not null) return existing;
        var now = _timeProvider.GetUtcNow();
        var association = new CampaignCharacterAssociation { Id = Guid.NewGuid(), CampaignId = campaignId, CharacterId = characterId, Status = CampaignCharacterAssociationStatus.Active, ConnectedAt = now };
        character.UpdatedAt = now;
        _dbContext.CampaignCharacters.Add(association);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return association;
    }

    public async Task DisconnectFromCampaignAsync(Guid ownerUserId, Guid characterId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var character = await GetOwnedCharacterForUpdateAsync(ownerUserId, characterId, cancellationToken);
        var association = await _dbContext.CampaignCharacters.SingleOrDefaultAsync(item => item.CharacterId == characterId && item.CampaignId == campaignId && item.Status == CampaignCharacterAssociationStatus.Active, cancellationToken);
        if (association is null) return;
        var now = _timeProvider.GetUtcNow();
        EndAssociation(association, ownerUserId, "Disconnected by character owner", now);
        character.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Character> GetOwnedCharacterForUpdateAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken) =>
        await _dbContext.Characters.SingleOrDefaultAsync(character => character.Id == characterId && character.OwnerUserId == ownerUserId, cancellationToken)
        ?? throw new CampaignDomainException("Character does not exist or is not owned by this account.");

    private static void EndAssociation(CampaignCharacterAssociation association, Guid endedByUserId, string reason, DateTimeOffset now)
    {
        association.Status = CampaignCharacterAssociationStatus.Ended;
        association.EndedAt = now;
        association.EndedByUserId = endedByUserId;
        association.EndReason = reason;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 160) throw new CampaignDomainException("Character names can not exceed 160 characters.");
        return normalized;
    }
}
