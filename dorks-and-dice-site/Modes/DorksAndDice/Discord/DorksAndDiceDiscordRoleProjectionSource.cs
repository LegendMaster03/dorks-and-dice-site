using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public sealed class DorksAndDiceDiscordRoleProjectionSource(
    DorksAndDiceDbContext dbContext,
    IModeExternalConnectionRegistry modeConnections) : IDiscordRoleProjectionSource
{
    public const string ProjectionSourceId = "dorks-and-dice";
    private const string ModeId = SiteModeValues.DorksAndDiceModeValue;

    public string SourceId => ProjectionSourceId;

    public async Task<IReadOnlyCollection<DiscordGuildRoleProjection>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        if (!modeConnections.TryGet(ModeId, DiscordProvider.Id, out var modeConnection)
            || modeConnection is null)
        {
            return [];
        }

        var campaigns = await dbContext.Campaigns
            .AsNoTracking()
            .Include(campaign => campaign.Memberships)
                .ThenInclude(membership => membership.Roles)
            .OrderBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);

        var bindings = await dbContext.CampaignDiscordGuildBindings
            .AsNoTracking()
            .ToDictionaryAsync(binding => binding.CampaignId, cancellationToken);

        var activeCampaigns = campaigns
            .Where(campaign => campaign.Status == CampaignStatus.Active)
            .ToArray();

        var generalRoles = new List<DiscordDesiredRole>();
        var playerUsers = activeCampaigns
            .SelectMany(campaign => campaign.Memberships)
            .Where(IsActiveMember)
            .Where(membership => membership.Roles.Any(role => role.Role == CampaignRoles.Player))
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArray();
        var dmUsers = activeCampaigns
            .SelectMany(campaign => campaign.Memberships)
            .Where(IsActiveMember)
            .Where(membership => membership.Roles.Any(role => role.Role == CampaignRoles.Dm))
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArray();

        generalRoles.Add(new DiscordDesiredRole(
            "general:player",
            "Player",
            playerUsers));
        generalRoles.Add(new DiscordDesiredRole(
            "general:dm",
            "DM",
            dmUsers));

        foreach (var campaign in activeCampaigns)
        {
            var members = campaign.Memberships
                .Where(IsActiveMember)
                .Select(membership => membership.UserId)
                .Distinct()
                .ToArray();
            generalRoles.Add(new DiscordDesiredRole(
                $"general:campaign:{campaign.Id:N}",
                CampaignRoleName(campaign.Name),
                members));
        }

        var result = new List<DiscordGuildRoleProjection>
        {
            new(
                ModeId,
                modeConnection.ResourceId,
                modeConnection.ResourceId,
                generalRoles)
        };

        foreach (var binding in bindings.Values.OrderBy(binding => binding.CampaignId))
        {
            var campaign = campaigns.SingleOrDefault(item => item.Id == binding.CampaignId);
            var dedicatedRoles = new List<DiscordDesiredRole>();

            if (campaign is { Status: CampaignStatus.Active })
            {
                var activeMembers = campaign.Memberships
                    .Where(IsActiveMember)
                    .ToArray();

                dedicatedRoles.Add(new DiscordDesiredRole(
                    $"campaign:{campaign.Id:N}:player",
                    "Player",
                    activeMembers
                        .Where(membership => membership.Roles.Any(
                            role => role.Role == CampaignRoles.Player))
                        .Select(membership => membership.UserId)
                        .Distinct()
                        .ToArray()));
                dedicatedRoles.Add(new DiscordDesiredRole(
                    $"campaign:{campaign.Id:N}:dm",
                    "DM",
                    activeMembers
                        .Where(membership => membership.Roles.Any(
                            role => role.Role == CampaignRoles.Dm))
                        .Select(membership => membership.UserId)
                        .Distinct()
                        .ToArray()));
            }

            result.Add(new DiscordGuildRoleProjection(
                ModeId,
                modeConnection.ResourceId,
                binding.GuildId,
                dedicatedRoles));
        }

        return result;
    }

    private static bool IsActiveMember(CampaignMembership membership) =>
        membership.Status == CampaignMembershipStatus.Active;

    private static string CampaignRoleName(string campaignName)
    {
        const string prefix = "Campaign: ";
        var available = DiscordManagedRoleNameLimit - prefix.Length;
        var normalized = campaignName.Trim();
        if (normalized.Length > available)
        {
            normalized = normalized[..available].TrimEnd();
        }

        return prefix + normalized;
    }

    private const int DiscordManagedRoleNameLimit = 100;
}
