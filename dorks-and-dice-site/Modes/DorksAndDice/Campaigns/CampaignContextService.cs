using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

/// <summary>
/// Stable, read-only campaign projections intended for consumers such as Rules Core,
/// Block Initiative, and other Dorks & Dice workflows. Consumers should depend on these
/// projections rather than persistence entities.
/// </summary>
public sealed record CampaignAccessContext(
    Guid CampaignId,
    string Name,
    IReadOnlyList<string> Roles);

public sealed record CampaignParticipantContext(
    Guid ParticipantId,
    string DisplayName,
    Guid? UserId);

public sealed record CampaignCharacterContext(
    Guid CharacterId,
    Guid OwnerUserId,
    string Name);

public sealed record CampaignContextSnapshot(
    Guid CampaignId,
    string Name,
    IReadOnlyList<string> RequestingUserRoles,
    IReadOnlyList<CampaignParticipantContext> Participants,
    IReadOnlyList<CampaignCharacterContext> Characters);

public interface ICampaignContextService
{
    Task<IReadOnlyList<CampaignAccessContext>> GetAccessibleCampaignsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CampaignContextSnapshot?> GetCampaignContextAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignContextService(
    DorksAndDiceDbContext dbContext,
    ICampaignAccessService campaignAccess) : ICampaignContextService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;

    public async Task<IReadOnlyList<CampaignAccessContext>> GetAccessibleCampaignsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _dbContext.CampaignMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Campaign.Status == CampaignStatus.Active)
            .Include(membership => membership.Campaign)
            .Include(membership => membership.Roles)
            .OrderBy(membership => membership.Campaign.Name)
            .ToListAsync(cancellationToken);

        return memberships
            .Select(membership => new CampaignAccessContext(
                membership.CampaignId,
                membership.Campaign.Name,
                membership.Roles.Select(role => role.Role).OrderBy(role => role).ToArray()))
            .ToArray();
    }

    public async Task<CampaignContextSnapshot?> GetCampaignContextAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await _campaignAccess.IsMemberAsync(userId, campaignId, cancellationToken))
        {
            return null;
        }

        var campaign = await _dbContext.Campaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == campaignId && item.Status == CampaignStatus.Active,
                cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var roles = await _dbContext.CampaignMembershipRoles
            .AsNoTracking()
            .Where(role => role.CampaignMembership.CampaignId == campaignId
                && role.CampaignMembership.UserId == userId
                && role.CampaignMembership.Status == CampaignMembershipStatus.Active)
            .Select(role => role.Role)
            .OrderBy(role => role)
            .ToListAsync(cancellationToken);

        var participants = await _dbContext.CampaignParticipants
            .AsNoTracking()
            .Where(participant => participant.CampaignId == campaignId
                && participant.Status == CampaignParticipantStatus.Active)
            .OrderBy(participant => participant.DisplayName)
            .Select(participant => new CampaignParticipantContext(
                participant.Id,
                participant.DisplayName,
                participant.UserId))
            .ToListAsync(cancellationToken);

        var characters = await _dbContext.CampaignCharacters
            .AsNoTracking()
            .Where(association => association.CampaignId == campaignId
                && association.Status == CampaignCharacterAssociationStatus.Active
                && association.Character.Status == CharacterStatus.Active)
            .OrderBy(association => association.Character.Name)
            .Select(association => new CampaignCharacterContext(
                association.CharacterId,
                association.Character.OwnerUserId,
                association.Character.Name))
            .ToListAsync(cancellationToken);

        return new CampaignContextSnapshot(
            campaign.Id,
            campaign.Name,
            roles,
            participants,
            characters);
    }
}
