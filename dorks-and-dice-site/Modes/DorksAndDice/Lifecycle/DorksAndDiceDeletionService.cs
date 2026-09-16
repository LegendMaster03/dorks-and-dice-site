using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;

public interface IDorksAndDiceDeletionService
{
    Task DeleteCharacterAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default);
    Task DeleteCampaignAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default);
}

public sealed class DorksAndDiceDeletionService(
    DorksAndDiceDbContext dbContext,
    TimeProvider timeProvider) : IDorksAndDiceDeletionService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task DeleteCharacterAsync(Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await _dbContext.Characters.SingleOrDefaultAsync(
            item => item.Id == characterId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new CampaignDomainException("Character does not exist or is not owned by this account.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var associations = await _dbContext.CampaignCharacters
                .Where(item => item.CharacterId == characterId)
                .ToListAsync(cancellationToken);

            if (associations.Count > 0)
            {
                _dbContext.CampaignCharacters.RemoveRange(associations);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _dbContext.Characters.Remove(character);
            _dbContext.ToolLifecycleOutboxEvents.Add(CreateLifecycleEvent(
                ToolLifecycleEventTypes.CharacterDeleted,
                characterId));
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteCampaignAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await _dbContext.Campaigns.SingleOrDefaultAsync(
            item => item.Id == campaignId,
            cancellationToken)
            ?? throw new CampaignDomainException("Campaign does not exist.");

        var isDm = await _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.UserId == actorUserId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Roles.Any(role => role.Role == CampaignRoles.Dm),
            cancellationToken);
        if (!isDm)
        {
            throw new CampaignDomainException("Campaign role 'dm' is required for this operation.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var associations = await _dbContext.CampaignCharacters
                .Where(item => item.CampaignId == campaignId)
                .ToListAsync(cancellationToken);
            var invitations = await _dbContext.CampaignInvitations
                .Where(item => item.CampaignId == campaignId)
                .ToListAsync(cancellationToken);

            if (associations.Count > 0)
            {
                _dbContext.CampaignCharacters.RemoveRange(associations);
            }
            if (invitations.Count > 0)
            {
                _dbContext.CampaignInvitations.RemoveRange(invitations);
            }
            if (associations.Count > 0 || invitations.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _dbContext.Campaigns.Remove(campaign);
            _dbContext.ToolLifecycleOutboxEvents.Add(CreateLifecycleEvent(
                ToolLifecycleEventTypes.CampaignDeleted,
                campaignId));
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private ToolLifecycleOutboxEvent CreateLifecycleEvent(string eventType, Guid subjectId)
    {
        var now = _timeProvider.GetUtcNow();
        return new ToolLifecycleOutboxEvent
        {
            EventId = Guid.NewGuid(),
            TargetToolSlug = ToolLifecycleTargets.CharacterSheet,
            EventType = eventType,
            SubjectId = subjectId,
            OccurredAt = now,
            AttemptCount = 0,
            NextAttemptAt = now
        };
    }
}
