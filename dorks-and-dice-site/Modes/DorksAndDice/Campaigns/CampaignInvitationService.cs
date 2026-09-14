using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignInvitationService
{
    Task<CampaignInvitationGrant> CreateAsync(
        Guid actorUserId,
        Guid campaignId,
        IEnumerable<string> roles,
        Guid? participantId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignInvitation>> GetPendingAsync(
        Guid actorUserId,
        Guid campaignId,
        CancellationToken cancellationToken = default);
    Task<CampaignInvitationPreview?> GetPreviewAsync(
        string token,
        CancellationToken cancellationToken = default);
    Task<CampaignMembership> AcceptAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);
    Task RevokeAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignInvitationService(
    DorksAndDiceDbContext dbContext,
    ICampaignAccessService campaignAccess,
    TimeProvider timeProvider) : ICampaignInvitationService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CampaignInvitationGrant> CreateAsync(
        Guid actorUserId,
        Guid campaignId,
        IEnumerable<string> roles,
        Guid? participantId = null,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var normalizedRoles = CampaignRoles.NormalizeMany(roles).OrderBy(role => role).ToArray();

        CampaignParticipant? participant = null;
        if (participantId is Guid requestedParticipantId)
        {
            participant = await _dbContext.CampaignParticipants.SingleOrDefaultAsync(
                item => item.Id == requestedParticipantId
                    && item.CampaignId == campaignId
                    && item.Status == CampaignParticipantStatus.Active,
                cancellationToken)
                ?? throw new CampaignDomainException("Active campaign participant does not exist.");

            if (participant.UserId is not null)
            {
                throw new CampaignDomainException("The selected participant is already linked to an account.");
            }
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = _timeProvider.GetUtcNow();
        var invitation = new CampaignInvitation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            ParticipantId = participant?.Id,
            TokenHash = HashToken(token),
            Roles = SerializeRoles(normalizedRoles),
            Status = CampaignInvitationStatus.Pending,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(DefaultLifetime)
        };

        _dbContext.CampaignInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignInvitationGrant(invitation, token);
    }

    public async Task<IReadOnlyList<CampaignInvitation>> GetPendingAsync(
        Guid actorUserId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        return await _dbContext.CampaignInvitations
            .AsNoTracking()
            .Include(invitation => invitation.Participant)
            .Where(invitation => invitation.CampaignId == campaignId
                && invitation.Status == CampaignInvitationStatus.Pending
                && invitation.ExpiresAt > now)
            .OrderBy(invitation => invitation.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CampaignInvitationPreview?> GetPreviewAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var now = _timeProvider.GetUtcNow();
        var invitation = await _dbContext.CampaignInvitations
            .AsNoTracking()
            .Include(item => item.Campaign)
            .Include(item => item.Participant)
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash
                    && item.Status == CampaignInvitationStatus.Pending
                    && item.ExpiresAt > now
                    && item.Campaign.Status == CampaignStatus.Active,
                cancellationToken);

        return invitation is null
            ? null
            : new CampaignInvitationPreview(
                invitation.Id,
                invitation.CampaignId,
                invitation.Campaign.Name,
                DeserializeRoles(invitation.Roles),
                invitation.Participant?.DisplayName,
                invitation.ExpiresAt);
    }

    public async Task<CampaignMembership> AcceptAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var now = _timeProvider.GetUtcNow();
        var invitation = await _dbContext.CampaignInvitations
            .Include(item => item.Campaign)
            .Include(item => item.Participant)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken)
            ?? throw new CampaignDomainException("Campaign invitation is invalid.");

        if (invitation.Status != CampaignInvitationStatus.Pending
            || invitation.ExpiresAt <= now
            || invitation.Campaign.Status != CampaignStatus.Active)
        {
            throw new CampaignDomainException("Campaign invitation is no longer available.");
        }

        var activeMembershipExists = await _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == invitation.CampaignId
                && membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active,
            cancellationToken);
        if (activeMembershipExists)
        {
            throw new CampaignDomainException("This account is already an active member of the campaign.");
        }

        if (invitation.Participant is not null)
        {
            if (invitation.Participant.Status != CampaignParticipantStatus.Active
                || invitation.Participant.UserId is not null)
            {
                throw new CampaignDomainException("The participant attached to this invitation is no longer available.");
            }

            var anotherLinkExists = await _dbContext.CampaignParticipants.AnyAsync(
                participant => participant.CampaignId == invitation.CampaignId
                    && participant.UserId == userId
                    && participant.Status == CampaignParticipantStatus.Active,
                cancellationToken);
            if (anotherLinkExists)
            {
                throw new CampaignDomainException("This account is already linked to another active participant in the campaign.");
            }
        }

        var membership = new CampaignMembership
        {
            Id = Guid.NewGuid(),
            CampaignId = invitation.CampaignId,
            UserId = userId,
            Status = CampaignMembershipStatus.Active,
            JoinedAt = now
        };

        foreach (var role in DeserializeRoles(invitation.Roles))
        {
            membership.Roles.Add(new CampaignMembershipRole
            {
                Id = Guid.NewGuid(),
                CampaignMembershipId = membership.Id,
                Role = role,
                GrantedAt = now,
                GrantedByUserId = invitation.CreatedByUserId
            });
        }

        if (invitation.Participant is not null)
        {
            invitation.Participant.UserId = userId;
        }

        invitation.Status = CampaignInvitationStatus.Accepted;
        invitation.AcceptedAt = now;
        invitation.AcceptedByUserId = userId;
        invitation.Campaign.UpdatedAt = now;
        _dbContext.CampaignMemberships.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CampaignDomainException("Campaign invitation is no longer available.", exception);
        }

        return membership;
    }

    public async Task RevokeAsync(
        Guid actorUserId,
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var invitation = await _dbContext.CampaignInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId && item.CampaignId == campaignId,
            cancellationToken)
            ?? throw new CampaignDomainException("Campaign invitation does not exist.");

        if (invitation.Status != CampaignInvitationStatus.Pending)
        {
            return;
        }

        invitation.Status = CampaignInvitationStatus.Revoked;
        invitation.RevokedAt = _timeProvider.GetUtcNow();
        invitation.RevokedByUserId = actorUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeRoles(IEnumerable<string> roles)
    {
        return string.Join(',', roles);
    }

    private static IReadOnlyList<string> DeserializeRoles(string roles)
    {
        return roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CampaignRoles.Normalize)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role)
            .ToArray();
    }

    private static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
