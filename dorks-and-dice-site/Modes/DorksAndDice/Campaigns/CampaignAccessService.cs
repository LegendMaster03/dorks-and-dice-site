using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

public interface ICampaignAccessService
{
    Task<bool> IsMemberAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAsync(Guid userId, Guid campaignId, string role, CancellationToken cancellationToken = default);
    Task RequireMemberAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken = default);
    Task RequireRoleAsync(Guid userId, Guid campaignId, string role, CancellationToken cancellationToken = default);
}

public sealed class CampaignAccessService(DorksAndDiceDbContext dbContext) : ICampaignAccessService
{
    private readonly DorksAndDiceDbContext _dbContext = dbContext;

    public Task<bool> IsMemberAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Campaign.Status == CampaignStatus.Active,
            cancellationToken);
    }

    public Task<bool> HasRoleAsync(
        Guid userId,
        Guid campaignId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = CampaignRoles.Normalize(role);
        return _dbContext.CampaignMemberships.AnyAsync(
            membership => membership.CampaignId == campaignId
                && membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active
                && membership.Campaign.Status == CampaignStatus.Active
                && membership.Roles.Any(grant => grant.Role == normalizedRole),
            cancellationToken);
    }

    public async Task RequireMemberAsync(
        Guid userId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsMemberAsync(userId, campaignId, cancellationToken))
        {
            throw new CampaignDomainException("Active campaign membership is required for this operation.");
        }
    }

    public async Task RequireRoleAsync(
        Guid userId,
        Guid campaignId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = CampaignRoles.Normalize(role);
        if (!await HasRoleAsync(userId, campaignId, normalizedRole, cancellationToken))
        {
            throw new CampaignDomainException(
                $"Campaign role '{normalizedRole}' is required for this operation.");
        }
    }
}
