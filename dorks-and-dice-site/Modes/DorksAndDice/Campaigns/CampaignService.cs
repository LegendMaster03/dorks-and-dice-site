using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignService
{
    Task<Campaign> CreateAsync(Guid creatorUserId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Campaign>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Campaign>> GetArchivedForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Campaign?> GetAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task RenameAsync(Guid actorUserId, Guid campaignId, string name, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<CampaignMembership> AddMemberAsync(Guid actorUserId, Guid campaignId, Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);
    Task SetMemberRolesAsync(Guid actorUserId, Guid campaignId, Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid actorUserId, Guid campaignId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class CampaignService(DorksAndDiceDbContext dbContext, ICampaignAccessService campaignAccess, TimeProvider timeProvider) : ICampaignService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Campaign> CreateAsync(Guid creatorUserId, string name, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(), Name = NormalizeName(name), CreatedByUserId = creatorUserId,
            Status = CampaignStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        var membership = new CampaignMembership
        {
            Id = Guid.NewGuid(), CampaignId = campaign.Id, UserId = creatorUserId,
            Status = CampaignMembershipStatus.Active, JoinedAt = now, Campaign = campaign
        };
        membership.Roles.Add(new CampaignMembershipRole
        {
            Id = Guid.NewGuid(), CampaignMembershipId = membership.Id, Role = CampaignRoles.Dm,
            GrantedAt = now, GrantedByUserId = creatorUserId, CampaignMembership = membership
        });
        campaign.Memberships.Add(membership);
        _dbContext.Campaigns.Add(campaign);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public Task<IReadOnlyList<Campaign>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetForUserByStatusAsync(userId, CampaignStatus.Active, cancellationToken);

    public Task<IReadOnlyList<Campaign>> GetArchivedForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetForUserByStatusAsync(userId, CampaignStatus.Archived, cancellationToken);

    private async Task<IReadOnlyList<Campaign>> GetForUserByStatusAsync(Guid userId, CampaignStatus status, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns.AsNoTracking()
            .Where(campaign => campaign.Status == status && campaign.Memberships.Any(membership =>
                membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active))
            .Include(campaign => campaign.Memberships.Where(membership =>
                membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active))
            .ThenInclude(membership => membership.Roles)
            .OrderBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Campaign?> GetAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Campaigns.AsNoTracking()
            .Where(campaign => campaign.Id == campaignId && campaign.Memberships.Any(membership =>
                membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active))
            .Include(campaign => campaign.Memberships).ThenInclude(membership => membership.Roles)
            .Include(campaign => campaign.Participants)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RenameAsync(Guid actorUserId, Guid campaignId, string name, CancellationToken cancellationToken = default)
    {
        await RequireDmIncludingArchivedAsync(actorUserId, campaignId, cancellationToken);
        var campaign = await GetCampaignForUpdateAsync(campaignId, cancellationToken);
        campaign.Name = NormalizeName(name);
        campaign.UpdatedAt = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var campaign = await GetCampaignForUpdateAsync(campaignId, cancellationToken);
        if (campaign.Status == CampaignStatus.Archived) return;
        var now = _timeProvider.GetUtcNow();
        campaign.Status = CampaignStatus.Archived;
        campaign.ArchivedAt = now;
        campaign.ArchivedByUserId = actorUserId;
        campaign.UpdatedAt = now;

        var associations = await _dbContext.CampaignCharacters
            .Where(item => item.CampaignId == campaignId && item.Status == CampaignCharacterAssociationStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var association in associations)
        {
            association.Status = CampaignCharacterAssociationStatus.Ended;
            association.EndedAt = now;
            association.EndedByUserId = actorUserId;
            association.EndReason = "Campaign archived";
        }

        var invitations = await _dbContext.CampaignInvitations
            .Where(item => item.CampaignId == campaignId && item.Status == CampaignInvitationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var invitation in invitations)
        {
            invitation.Status = CampaignInvitationStatus.Revoked;
            invitation.RevokedAt = now;
            invitation.RevokedByUserId = actorUserId;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await RequireDmIncludingArchivedAsync(actorUserId, campaignId, cancellationToken);
        var campaign = await GetCampaignForUpdateAsync(campaignId, cancellationToken);
        if (campaign.Status == CampaignStatus.Active) return;
        campaign.Status = CampaignStatus.Active;
        campaign.ArchivedAt = null;
        campaign.ArchivedByUserId = null;
        campaign.UpdatedAt = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CampaignMembership> AddMemberAsync(Guid actorUserId, Guid campaignId, Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var normalizedRoles = CampaignRoles.NormalizeMany(roles);
        if (await _dbContext.CampaignMemberships.AnyAsync(membership => membership.CampaignId == campaignId && membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active, cancellationToken))
            throw new CampaignDomainException("The account is already an active member of this campaign.");
        var now = _timeProvider.GetUtcNow();
        var membership = new CampaignMembership
        {
            Id = Guid.NewGuid(), CampaignId = campaignId, UserId = userId,
            Status = CampaignMembershipStatus.Active, JoinedAt = now
        };
        foreach (var role in normalizedRoles)
        {
            membership.Roles.Add(new CampaignMembershipRole
            {
                Id = Guid.NewGuid(), CampaignMembershipId = membership.Id, Role = role,
                GrantedAt = now, GrantedByUserId = actorUserId
            });
        }
        _dbContext.CampaignMemberships.Add(membership);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    public async Task SetMemberRolesAsync(Guid actorUserId, Guid campaignId, Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var normalizedRoles = CampaignRoles.NormalizeMany(roles).ToHashSet(StringComparer.Ordinal);
        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        var existingRoles = membership.Roles.ToArray();
        var hadDmRole = existingRoles.Any(role => role.Role == CampaignRoles.Dm);
        var hadPlayerRole = existingRoles.Any(role => role.Role == CampaignRoles.Player);
        if (hadDmRole && !normalizedRoles.Contains(CampaignRoles.Dm)) await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);

        var rolesToRemove = existingRoles.Where(role => !normalizedRoles.Contains(role.Role)).ToArray();
        var existingRoleNames = existingRoles.Select(role => role.Role).ToHashSet(StringComparer.Ordinal);
        var rolesToAdd = normalizedRoles.Where(role => !existingRoleNames.Contains(role)).ToArray();
        var now = _timeProvider.GetUtcNow();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (rolesToRemove.Length > 0)
            {
                var roleIds = rolesToRemove.Select(role => role.Id).ToArray();
                await _dbContext.CampaignMembershipRoles.Where(role => roleIds.Contains(role.Id)).ExecuteDeleteAsync(cancellationToken);
                foreach (var role in rolesToRemove)
                {
                    membership.Roles.Remove(role);
                    _dbContext.Entry(role).State = EntityState.Detached;
                }
            }
            foreach (var role in rolesToAdd)
            {
                var membershipRole = new CampaignMembershipRole
                {
                    Id = Guid.NewGuid(), CampaignMembershipId = membership.Id, Role = role,
                    GrantedAt = now, GrantedByUserId = actorUserId, CampaignMembership = membership
                };
                membership.Roles.Add(membershipRole);
                _dbContext.CampaignMembershipRoles.Add(membershipRole);
            }
            if (hadPlayerRole && !normalizedRoles.Contains(CampaignRoles.Player))
                await EndCharacterAssociationsAsync(campaignId, userId, actorUserId, "Player role removed", now, cancellationToken);
            await TouchCampaignAsync(campaignId, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task LeaveAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await EnsureCampaignActiveAsync(campaignId, cancellationToken);
        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        if (membership.Roles.Any(role => role.Role == CampaignRoles.Dm)) await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        EndMembership(membership, CampaignMembershipStatus.Left, userId, "Left campaign", now);
        await EndCharacterAssociationsAsync(campaignId, userId, userId, "Owner left campaign", now, cancellationToken);
        await EndParticipantLinksAsync(campaignId, userId, userId, "Linked account left campaign", now, cancellationToken);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid actorUserId, Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var membership = await GetActiveMembershipAsync(campaignId, userId, cancellationToken);
        if (membership.Roles.Any(role => role.Role == CampaignRoles.Dm)) await EnsureAnotherDmExistsAsync(campaignId, membership.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        EndMembership(membership, CampaignMembershipStatus.Removed, actorUserId, "Removed by campaign DM", now);
        await EndCharacterAssociationsAsync(campaignId, userId, actorUserId, "Owner removed from campaign", now, cancellationToken);
        await EndParticipantLinksAsync(campaignId, userId, actorUserId, "Linked account removed from campaign", now, cancellationToken);
        await TouchCampaignAsync(campaignId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireDmIncludingArchivedAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken)
    {
        var allowed = await _dbContext.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId && membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active
            && membership.Roles.Any(role => role.Role == CampaignRoles.Dm), cancellationToken);
        if (!allowed) throw new CampaignDomainException("Campaign role 'dm' is required for this operation.");
    }

    private async Task EnsureCampaignActiveAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId && campaign.Status == CampaignStatus.Active, cancellationToken))
            throw new CampaignDomainException("This operation requires an active campaign.");
    }

    private async Task<Campaign> GetCampaignForUpdateAsync(Guid campaignId, CancellationToken cancellationToken) =>
        await _dbContext.Campaigns.SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken)
        ?? throw new CampaignDomainException("Campaign does not exist.");

    private async Task<CampaignMembership> GetActiveMembershipAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        await _dbContext.CampaignMemberships.Include(membership => membership.Roles).SingleOrDefaultAsync(
            membership => membership.CampaignId == campaignId && membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active,
            cancellationToken) ?? throw new CampaignDomainException("The account is not an active member of this campaign.");

    private async Task EnsureAnotherDmExistsAsync(Guid campaignId, Guid excludedMembershipId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId && membership.Id != excludedMembershipId
            && membership.Status == CampaignMembershipStatus.Active && membership.Roles.Any(role => role.Role == CampaignRoles.Dm), cancellationToken);
        if (!exists) throw new CampaignDomainException("A campaign must retain at least one active DM.");
    }

    private async Task EndCharacterAssociationsAsync(Guid campaignId, Guid ownerUserId, Guid endedByUserId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var associations = await _dbContext.CampaignCharacters.Include(association => association.Character)
            .Where(association => association.CampaignId == campaignId && association.Status == CampaignCharacterAssociationStatus.Active && association.Character.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);
        foreach (var association in associations)
        {
            association.Status = CampaignCharacterAssociationStatus.Ended;
            association.EndedAt = now;
            association.EndedByUserId = endedByUserId;
            association.EndReason = reason;
        }
    }

    private async Task EndParticipantLinksAsync(Guid campaignId, Guid userId, Guid endedByUserId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var participants = await _dbContext.CampaignParticipants.Where(participant =>
            participant.CampaignId == campaignId && participant.UserId == userId && participant.Status == CampaignParticipantStatus.Active).ToListAsync(cancellationToken);
        foreach (var participant in participants)
        {
            participant.Status = CampaignParticipantStatus.Former;
            participant.EndedAt = now;
            participant.EndedByUserId = endedByUserId;
            participant.EndReason = reason;
        }
    }

    private async Task TouchCampaignAsync(Guid campaignId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var campaign = await GetCampaignForUpdateAsync(campaignId, cancellationToken);
        campaign.UpdatedAt = now;
    }

    private static void EndMembership(CampaignMembership membership, CampaignMembershipStatus status, Guid endedByUserId, string reason, DateTimeOffset now)
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
        if (normalized.Length > 160) throw new CampaignDomainException("Campaign names can not exceed 160 characters.");
        return normalized;
    }
}
