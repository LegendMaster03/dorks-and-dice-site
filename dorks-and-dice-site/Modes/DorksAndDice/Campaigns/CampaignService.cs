using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignService
{
    Task<Campaign> CreateAsync(Guid creatorUserId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Campaign>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Campaign?> GetAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<CampaignMembership> AddMemberAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);
    Task SetMemberRolesAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignService(
    DorksAndDiceDbContext dbContext,
    ICampaignAccessService campaignAccess,
    TimeProvider timeProvider) : ICampaignService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Campaign> CreateAsync(
        Guid creatorUserId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var campaignName = NormalizeName(name);
        var now = _timeProvider.GetUtcNow();

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = campaignName,
            CreatedByUserId = creatorUserId,
            Status = CampaignStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var membership = new CampaignMembership
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            UserId = creatorUserId,
            Status = CampaignMembershipStatus.Active,
            JoinedAt = now,
            Campaign = campaign
        };
        membership.Roles.Add(new CampaignMembershipRole
        {
            Id = Guid.NewGuid(),
            CampaignMembershipId = membership.Id,
            Role = CampaignRoles.Dm,
            GrantedAt = now,
            GrantedByUserId = creatorUserId,
            CampaignMembership = membership
        });
        campaign.Memberships.Add(membership);

        _dbContext.Campaigns.Add(campaign);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<IReadOnlyList<Campaign>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.Status == CampaignStatus.Active
                && campaign.Memberships.Any(membership =>
                    membership.UserId == userId
                    && membership.Status == CampaignMembershipStatus.Active))
            .Include(campaign => campaign.Memberships.Where(membership =>
                membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active))
                .ThenInclude(membership => membership.Roles)
            .OrderBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Campaign?> GetAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.Id == campaignId
                && campaign.Memberships.Any(membership =>
                    membership.UserId == userId
                    && membership.Status == CampaignMembershipStatus.Active))
            .Include(campaign => campaign.Memberships)
                .ThenInclude(membership => membership.Roles)
            .Include(campaign => campaign.Participants)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CampaignMembership> AddMemberAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var normalizedRoles = CampaignRoles.NormalizeMany(roles);

        var activeMembershipExists = await _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active,
            cancellationToken);
        if (activeMembershipExists)
        {
            throw new CampaignDomainException("The account is already an active member of this campaign.");
        }

        var now = _timeProvider.GetUtcNow();
        var membership = new CampaignMembership
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = userId,
            Status = CampaignMembershipStatus.Active,
            JoinedAt = now
        };

        foreach (var role in normalizedRoles)
        {
            membership.Roles.Add(new CampaignMembershipRole
            {
                Id = Guid.NewGuid(),
                CampaignMembershipId = membership.Id,
                Role = role,
                GrantedAt = now,
                GrantedByUserId = actorUserId
            });
        }

        _dbContext.CampaignMemberships.Add(membership);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task SetMemberRolesAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var normalizedRoles = CampaignRoles.NormalizeMany(roles);

        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        if (membership.Roles.Any(role => role.Role == CampaignRoles.Dm)
            && !normalizedRoles.Contains(CampaignRoles.Dm, StringComparer.Ordinal))
        {
            await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        _dbContext.CampaignMembershipRoles.RemoveRange(membership.Roles);
        membership.Roles.Clear();

        foreach (var role in normalizedRoles)
        {
            membership.Roles.Add(new CampaignMembershipRole
            {
                Id = Guid.NewGuid(),
                CampaignMembershipId = membership.Id,
                Role = role,
                GrantedAt = now,
                GrantedByUserId = actorUserId
            });
        }

        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        if (membership.Roles.Any(role => role.Role == CampaignRoles.Dm))
        {
            await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        EndMembership(membership, CampaignMembershipStatus.Left, userId, "Left campaign", now);
        await EndCharacterAssociationsAsync(campaignId, userId, userId, "Owner left campaign", now, cancellationToken);
        await EndParticipantLinksAsync(campaignId, userId, userId, "Linked account left campaign", now, cancellationToken);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        if (membership.Roles.Any(role => role.Role == CampaignRoles.Dm))
        {
            await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        EndMembership(membership, CampaignMembershipStatus.Removed, actorUserId, "Removed by campaign DM", now);
        await EndCharacterAssociationsAsync(campaignId, userId, actorUserId, "Owner removed from campaign", now, cancellationToken);
        await EndParticipantLinksAsync(campaignId, userId, actorUserId, "Linked account removed from campaign", now, cancellationToken);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CampaignMembership> GetActiveMembershipAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CampaignMemberships
            .Include(membership => membership.Roles)
            .SingleOrDefaultAsync(
                membership => membership.CampaignId == campaignId
                    && membership.UserId == userId
                    && membership.Status == CampaignMembershipStatus.Active,
                cancellationToken)
            ?? throw new CampaignDomainException("The account is not an active member of this campaign.");
    }

    private async Task EnsureAnotherDmExistsAsync(
        Guid campaignId,
        Guid excludedMembershipId,
        CancellationToken cancellationToken)
    {
        var anotherDmExists = await _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.Id != excludedMembershipId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Roles.Any(role => role.Role == CampaignRoles.Dm),
            cancellationToken);
        if (!anotherDmExists)
        {
            throw new CampaignDomainException("A campaign must retain at least one active DM.");
        }
    }

    private async Task EndCharacterAssociationsAsync(
        Guid campaignId,
        Guid ownerUserId,
        Guid endedByUserId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var associations = await _dbContext.CampaignCharacters
            .Include(association => association.Character)
            .Where(association => association.CampaignId == campaignId
                && association.Status == CampaignCharacterAssociationStatus.Active
                && association.Character.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

        foreach (var association in associations)
        {
            association.Status = CampaignCharacterAssociationStatus.Ended;
            association.EndedAt = now;
            association.EndedByUserId = endedByUserId;
            association.EndReason = reason;
        }
    }

    private async Task EndParticipantLinksAsync(
        Guid campaignId,
        Guid userId,
        Guid endedByUserId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var participants = await _dbContext.CampaignParticipants
            .Where(participant => participant.CampaignId == campaignId
                && participant.UserId == userId
                && participant.Status == CampaignParticipantStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var participant in participants)
        {
            participant.Status = CampaignParticipantStatus.Former;
            participant.EndedAt = now;
            participant.EndedByUserId = endedByUserId;
            participant.EndReason = reason;
        }
    }

    private async Task TouchCampaignAsync(
        Guid campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var campaign = await _dbContext.Campaigns.SingleOrDefaultAsync(
            item => item.Id == campaignId,
            cancellationToken)
            ?? throw new CampaignDomainException("Campaign does not exist.");
        campaign.UpdatedAt = now;
    }

    private static void EndMembership(
        CampaignMembership membership,
        CampaignMembershipStatus status,
        Guid endedByUserId,
        string reason,
        DateTimeOffset now)
    {
        membership.Status = status;
        membership.EndedAt = now;
        membership.EndedByUserId = endedByUserId;
        membership.EndReason = reason;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 160)
        {
            throw new CampaignDomainException("Campaign names can not exceed 160 characters.");
        }

        return normalized;
    }
}
