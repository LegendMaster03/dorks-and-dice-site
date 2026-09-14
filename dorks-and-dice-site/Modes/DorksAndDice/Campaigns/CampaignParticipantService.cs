using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignParticipantService
{
    Task<IReadOnlyList<CampaignParticipant>> GetForCampaignAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<CampaignParticipant> AddGuestAsync(Guid actorUserId, Guid campaignId, string displayName, CancellationToken cancellationToken = default);
    Task RenameAsync(Guid actorUserId, Guid campaignId, Guid participantId, string displayName, CancellationToken cancellationToken = default);
    Task LinkToUserAsync(Guid actorUserId, Guid campaignId, Guid participantId, Guid userId, CancellationToken cancellationToken = default);
    Task RetireAsync(Guid actorUserId, Guid campaignId, Guid participantId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid actorUserId, Guid campaignId, Guid participantId, CancellationToken cancellationToken = default);
}

public sealed class CampaignParticipantService(DorksAndDiceDbContext dbContext, ICampaignAccessService campaignAccess, TimeProvider timeProvider) : ICampaignParticipantService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyList<CampaignParticipant>> GetForCampaignAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireMemberAsync(userId, campaignId, cancellationToken);
        return await _dbContext.CampaignParticipants.AsNoTracking().Where(participant => participant.CampaignId == campaignId)
            .OrderBy(participant => participant.Status).ThenBy(participant => participant.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<CampaignParticipant> AddGuestAsync(Guid actorUserId, Guid campaignId, string displayName, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var participant = new CampaignParticipant
        {
            Id = Guid.NewGuid(), CampaignId = campaignId, DisplayName = NormalizeDisplayName(displayName),
            Status = CampaignParticipantStatus.Active, CreatedAt = _timeProvider.GetUtcNow()
        };
        _dbContext.CampaignParticipants.Add(participant);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return participant;
    }

    public async Task RenameAsync(Guid actorUserId, Guid campaignId, Guid participantId, string displayName, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var participant = await GetParticipantForUpdateAsync(campaignId, participantId, cancellationToken);
        participant.DisplayName = NormalizeDisplayName(displayName);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LinkToUserAsync(Guid actorUserId, Guid campaignId, Guid participantId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        await _campaignAccess.RequireMemberAsync(userId, campaignId, cancellationToken);
        var participant = await GetActiveParticipantForUpdateAsync(campaignId, participantId, cancellationToken);
        var anotherLinkExists = await _dbContext.CampaignParticipants.AnyAsync(item =>
            item.CampaignId == campaignId && item.Id != participantId && item.UserId == userId && item.Status == CampaignParticipantStatus.Active,
            cancellationToken);
        if (anotherLinkExists) throw new CampaignDomainException("This account is already linked to another active participant in the campaign.");
        participant.UserId = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RetireAsync(Guid actorUserId, Guid campaignId, Guid participantId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var participant = await GetActiveParticipantForUpdateAsync(campaignId, participantId, cancellationToken);
        participant.Status = CampaignParticipantStatus.Former;
        participant.EndedAt = _timeProvider.GetUtcNow();
        participant.EndedByUserId = actorUserId;
        participant.EndReason = "Retired by campaign DM";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid actorUserId, Guid campaignId, Guid participantId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var participant = await GetParticipantForUpdateAsync(campaignId, participantId, cancellationToken);
        if (participant.Status == CampaignParticipantStatus.Active) return;
        if (participant.UserId is Guid linkedUserId && !await _campaignAccess.IsMemberAsync(linkedUserId, campaignId, cancellationToken))
            throw new CampaignDomainException("A participant linked to an account can only be restored after that account rejoins the campaign.");
        participant.Status = CampaignParticipantStatus.Active;
        participant.EndedAt = null;
        participant.EndedByUserId = null;
        participant.EndReason = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CampaignParticipant> GetParticipantForUpdateAsync(Guid campaignId, Guid participantId, CancellationToken cancellationToken) =>
        await _dbContext.CampaignParticipants.SingleOrDefaultAsync(participant => participant.Id == participantId && participant.CampaignId == campaignId, cancellationToken)
        ?? throw new CampaignDomainException("Campaign participant does not exist.");

    private async Task<CampaignParticipant> GetActiveParticipantForUpdateAsync(Guid campaignId, Guid participantId, CancellationToken cancellationToken) =>
        await _dbContext.CampaignParticipants.SingleOrDefaultAsync(participant => participant.Id == participantId && participant.CampaignId == campaignId && participant.Status == CampaignParticipantStatus.Active, cancellationToken)
        ?? throw new CampaignDomainException("Active campaign participant does not exist.");

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length > 120) throw new CampaignDomainException("Participant display names can not exceed 120 characters.");
        return normalized;
    }
}
