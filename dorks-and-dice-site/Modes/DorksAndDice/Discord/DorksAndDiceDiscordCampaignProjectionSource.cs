using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public sealed class DorksAndDiceDiscordCampaignProjectionSource(
    DorksAndDiceDbContext dbContext,
    IModeExternalConnectionRegistry modeConnections) : IDiscordWorkspaceProjectionSource
{
    public const string ProjectionSourceId = "dorks-and-dice-campaigns";
    private const string ModeId = SiteModeValues.DorksAndDiceModeValue;
    private const int DiscordManagedRoleNameLimit = 100;

    public string SourceId => ProjectionSourceId;

    public async Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
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

        var activeCampaigns = campaigns
            .Where(campaign => campaign.Status == CampaignStatus.Active)
            .ToArray();

        var result = new List<DiscordGuildWorkspaceProjection>
        {
            new(
                ModeId,
                modeConnection.ResourceId,
                modeConnection.ResourceId,
                BuildRoles("main", activeCampaigns, includeCampaignRoles: true),
                [])
        };

        var bindings = await dbContext.DiscordServerBindings
            .AsNoTracking()
            .Include(binding => binding.Campaigns)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var binding in bindings)
        {
            var dmCampaigns = activeCampaigns
                .Where(campaign => IsDm(campaign, binding.OwnerUserId))
                .ToDictionary(campaign => campaign.Id);

            Campaign[] selectedCampaigns;
            if (binding.CampaignScope == DiscordServerCampaignScope.AllDmCampaigns)
            {
                selectedCampaigns = dmCampaigns.Values
                    .OrderBy(campaign => campaign.Name)
                    .ToArray();
            }
            else
            {
                selectedCampaigns = binding.Campaigns
                    .Select(selection => selection.CampaignId)
                    .Distinct()
                    .Where(dmCampaigns.ContainsKey)
                    .Select(campaignId => dmCampaigns[campaignId])
                    .OrderBy(campaign => campaign.Name)
                    .ToArray();
            }

            var includeCampaignRoles =
                binding.CampaignScope != DiscordServerCampaignScope.SingleCampaign;

            result.Add(new DiscordGuildWorkspaceProjection(
                ModeId,
                modeConnection.ResourceId,
                binding.GuildId,
                BuildRoles(
                    $"server:{binding.Id:N}",
                    selectedCampaigns,
                    includeCampaignRoles),
                []));
        }

        return result;
    }

    private static IReadOnlyCollection<DiscordDesiredRole> BuildRoles(
        string keyPrefix,
        IReadOnlyCollection<Campaign> campaigns,
        bool includeCampaignRoles)
    {
        var roles = new List<DiscordDesiredRole>();
        var memberships = campaigns
            .SelectMany(campaign => campaign.Memberships)
            .Where(IsActiveMember)
            .ToArray();

        roles.Add(new DiscordDesiredRole(
            $"{keyPrefix}:player",
            "Player",
            memberships
                .Where(membership => membership.Roles.Any(
                    role => role.Role == CampaignRoles.Player))
                .Select(membership => membership.UserId)
                .Distinct()
                .ToArray()));

        roles.Add(new DiscordDesiredRole(
            $"{keyPrefix}:dm",
            "DM",
            memberships
                .Where(membership => membership.Roles.Any(
                    role => role.Role == CampaignRoles.Dm))
                .Select(membership => membership.UserId)
                .Distinct()
                .ToArray()));

        if (includeCampaignRoles)
        {
            foreach (var campaign in campaigns.OrderBy(campaign => campaign.Name))
            {
                roles.Add(new DiscordDesiredRole(
                    $"{keyPrefix}:campaign:{campaign.Id:N}",
                    CampaignRoleName(campaign.Name),
                    campaign.Memberships
                        .Where(IsActiveMember)
                        .Select(membership => membership.UserId)
                        .Distinct()
                        .ToArray()));
            }
        }

        return roles;
    }

    private static bool IsActiveMember(CampaignMembership membership) =>
        membership.Status == CampaignMembershipStatus.Active;

    private static bool IsDm(Campaign campaign, Guid userId) =>
        campaign.Memberships.Any(membership =>
            membership.UserId == userId
            && IsActiveMember(membership)
            && membership.Roles.Any(role => role.Role == CampaignRoles.Dm));

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
}
