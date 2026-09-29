using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignInvitationService
{
    Task<CampaignInvitationGrant> CreateAsync(Guid actorUserId, Guid campaignId, IEnumerable<string> roles, bool isReusable = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignInvitation>> GetPendingAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<CampaignInvitationPreview?> GetPreviewAsync(string token, CancellationToken cancellationToken = default);
    Task<CampaignMembership> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid actorUserId, Guid campaignId, Guid invitationId, CancellationToken cancellationToken = default);
}

public sealed class CampaignInvitationService(DorksAndDiceDbContext dbContext, ICampaignAccessService campaignAccess, TimeProvider timeProvider) : ICampaignInvitationService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private readonly DorksAndDiceDbContext _dbContext = dbContext;
    private readonly ICampaignAccessService _campaignAccess = campaignAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CampaignInvitationGrant> CreateAsync(Guid actorUserId, Guid campaignId, IEnumerable<string> roles, bool isReusable = false, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        await ExpireStalePendingInvitationsAsync(campaignId, now, cancellationToken);

        var normalizedRoles = CampaignRoles.NormalizeMany(roles).OrderBy(role => role).ToArray();
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var invitation = new CampaignInvitation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            TokenHash = HashToken(token),
            Roles = SerializeRoles(normalizedRoles),
            IsReusable = isReusable,
            Status = CampaignInvitationStatus.Pending,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(DefaultLifetime)
        };

        _dbContext.CampaignInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignInvitationGrant(invitation, token);
    }

    public async Task<IReadOnlyList<CampaignInvitation>> GetPendingAsync(Guid actorUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await _campaignAccess.RequireRoleAsync(actorUserId, campaignId, CampaignRoles.Dm, cancellationToken);
        await ExpireStalePendingInvitationsAsync(campaignId, _timeProvider.GetUtcNow(), cancellationToken);

        return await _dbContext.CampaignInvitations
            .AsNoTracking()
            .Where(invitation => invitation.CampaignId == campaignId && invitation.Status == CampaignInvitationStatus.Pending)
            .OrderBy(invitation => invitation.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CampaignInvitationPreview?> GetPreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.CampaignInvitations
            .Include(item => item.Campaign)
            .SingleOrDefaultAsync(item => item.TokenHash == HashToken(token), cancellationToken);

        if (invitation is null || invitation.Status != CampaignInvitationStatus.Pending)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (invitation.ExpiresAt <= now)
        {
            invitation.Status = CampaignInvitationStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (invitation.Campaign.Status != CampaignStatus.Active)
        {
            return null;
        }

        return new CampaignInvitationPreview(
            invitation.Id,
            invitation.CampaignId,
            invitation.Campaign.Name,
            DeserializeRoles(invitation.Roles),
            invitation.IsReusable,
            invitation.ExpiresAt);
    }

    public async Task<CampaignMembership> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var invitation = await _dbContext.CampaignInvitations
            .Include(item => item.Campaign)
            .SingleOrDefaultAsync(item => item.TokenHash == HashToken(token), cancellationToken)
            ?? throw new CampaignDomainException("Campaign invitation is invalid.");

        if (invitation.Status == CampaignInvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            invitation.Status = CampaignInvitationStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new CampaignDomainException("Campaign invitation is no longer available.");
        }

        if (invitation.Status != CampaignInvitationStatus.Pending || invitation.Campaign.Status != CampaignStatus.Active)
        {
            throw new CampaignDomainException("Campaign invitation is no longer available.");
        }

        if (await _dbContext.CampaignMemberships.AnyAsync(
                membership => membership.CampaignId == invitation.CampaignId
                    && membership.UserId == userId
                    && membership.Status == CampaignMembershipStatus.Active,
                cancellationToken))
        {
            throw new CampaignDomainException("This account is already an active member of the campaign.");
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

        if (invitation.IsReusable)
        {
            // Force an update against the Status concurrency token without consuming the link.
            // A concurrent revoke or expiration therefore causes this redemption to fail cleanly.
            _dbContext.Entry(invitation).Property(item => item.Status).IsModified = true;
        }
        else
        {
            invitation.Status = CampaignInvitationStatus.Accepted;
            invitation.AcceptedAt = now;
            invitation.AcceptedByUserId = userId;
        }

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

    public async Task RevokeAsync(Guid actorUserId, Guid campaignId, Guid invitationId, CancellationToken cancellationToken = default)
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

        var now = _timeProvider.GetUtcNow();
        if (invitation.ExpiresAt <= now)
        {
            invitation.Status = CampaignInvitationStatus.Expired;
        }
        else
        {
            invitation.Status = CampaignInvitationStatus.Revoked;
            invitation.RevokedAt = now;
            invitation.RevokedByUserId = actorUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireStalePendingInvitationsAsync(Guid campaignId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stale = await _dbContext.CampaignInvitations
            .Where(invitation => invitation.CampaignId == campaignId
                && invitation.Status == CampaignInvitationStatus.Pending
                && invitation.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return;
        }

        foreach (var invitation in stale)
        {
            invitation.Status = CampaignInvitationStatus.Expired;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeRoles(IEnumerable<string> roles) => string.Join(',', roles);

    private static IReadOnlyList<string> DeserializeRoles(string roles) => roles
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(CampaignRoles.Normalize)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(role => role)
        .ToArray();

    private static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
