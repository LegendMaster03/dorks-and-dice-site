using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public interface ICampaignDiscordGuildService
{
    Task<CampaignDiscordGuildBinding?> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        Guid actorUserId,
        Guid campaignId,
        string guildId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        Guid actorUserId,
        Guid campaignId,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignDiscordGuildService(
    DorksAndDiceDbContext dbContext,
    ICampaignAccessService campaignAccess,
    IModeExternalConnectionRegistry modeConnections,
    TimeProvider timeProvider) : ICampaignDiscordGuildService
{
    public Task<CampaignDiscordGuildBinding?> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        dbContext.CampaignDiscordGuildBindings
            .AsNoTracking()
            .SingleOrDefaultAsync(binding => binding.CampaignId == campaignId, cancellationToken);

    public async Task SetAsync(
        Guid actorUserId,
        Guid campaignId,
        string guildId,
        CancellationToken cancellationToken = default)
    {
        await campaignAccess.RequireRoleAsync(
            actorUserId,
            campaignId,
            CampaignRoles.Dm,
            cancellationToken);

        var normalizedGuildId = NormalizeGuildId(guildId);
        if (modeConnections.TryGet(
                SiteModeValues.DorksAndDiceModeValue,
                DiscordProvider.Id,
                out var mainConnection)
            && string.Equals(
                mainConnection?.ResourceId,
                normalizedGuildId,
                StringComparison.Ordinal))
        {
            throw new CampaignDomainException(
                "The dedicated campaign Discord server must be different from the main Dorks & Dice server.");
        }

        if (await dbContext.CampaignDiscordGuildBindings.AnyAsync(
                item => item.GuildId == normalizedGuildId
                    && item.CampaignId != campaignId,
                cancellationToken))
        {
            throw new CampaignDomainException(
                "That Discord server is already assigned to another campaign.");
        }

        var now = timeProvider.GetUtcNow();
        var binding = await dbContext.CampaignDiscordGuildBindings
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId, cancellationToken);

        if (binding is null)
        {
            dbContext.CampaignDiscordGuildBindings.Add(new CampaignDiscordGuildBinding
            {
                CampaignId = campaignId,
                GuildId = normalizedGuildId,
                ConfiguredByUserId = actorUserId,
                ConfiguredAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            binding.GuildId = normalizedGuildId;
            binding.ConfiguredByUserId = actorUserId;
            binding.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(
        Guid actorUserId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await campaignAccess.RequireRoleAsync(
            actorUserId,
            campaignId,
            CampaignRoles.Dm,
            cancellationToken);

        var binding = await dbContext.CampaignDiscordGuildBindings
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId, cancellationToken);
        if (binding is null)
        {
            return;
        }

        dbContext.CampaignDiscordGuildBindings.Remove(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeGuildId(string guildId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guildId);
        var normalized = guildId.Trim();
        if (normalized.Length > CampaignDiscordGuildBinding.GuildIdMaxLength
            || normalized.Any(character => character is < '0' or > '9'))
        {
            throw new CampaignDomainException(
                "Discord server ID must contain only digits.");
        }

        return normalized;
    }
}
