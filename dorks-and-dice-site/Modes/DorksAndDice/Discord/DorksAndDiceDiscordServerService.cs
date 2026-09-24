using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public interface IDorksAndDiceDiscordServerService
{
    Task<IReadOnlyList<DorksAndDiceDiscordServerBinding>> GetForOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campaign>> GetEligibleCampaignsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<DorksAndDiceDiscordServerBinding> ConfigureAsync(
        Guid ownerUserId,
        Guid? bindingId,
        string guildId,
        DiscordServerCampaignScope scope,
        IReadOnlyCollection<Guid> campaignIds,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid ownerUserId,
        Guid bindingId,
        CancellationToken cancellationToken = default);
}

public sealed class DorksAndDiceDiscordServerService(
    DorksAndDiceDbContext dbContext,
    IdentityDbContext identityDbContext,
    IModeExternalConnectionRegistry modeConnections,
    IDiscordGuildOwnershipVerifier ownershipVerifier,
    TimeProvider timeProvider) : IDorksAndDiceDiscordServerService
{
    private const string ModeId = SiteModeValues.DorksAndDiceModeValue;

    public async Task<IReadOnlyList<DorksAndDiceDiscordServerBinding>> GetForOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.DiscordServerBindings
            .AsNoTracking()
            .Where(binding => binding.OwnerUserId == ownerUserId)
            .Include(binding => binding.Campaigns)
                .ThenInclude(selection => selection.Campaign)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Campaign>> GetEligibleCampaignsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.Status == CampaignStatus.Active
                && campaign.Memberships.Any(membership =>
                    membership.UserId == ownerUserId
                    && membership.Status == CampaignMembershipStatus.Active
                    && membership.Roles.Any(role => role.Role == CampaignRoles.Dm)))
            .OrderBy(campaign => campaign.Name)
            .ToListAsync(cancellationToken);

    public async Task<DorksAndDiceDiscordServerBinding> ConfigureAsync(
        Guid ownerUserId,
        Guid? bindingId,
        string guildId,
        DiscordServerCampaignScope scope,
        IReadOnlyCollection<Guid> campaignIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedGuildId = NormalizeGuildId(guildId);
        ValidateScope(scope);

        if (!modeConnections.TryGet(ModeId, DiscordProvider.Id, out var modeConnection)
            || modeConnection is null)
        {
            throw new CampaignDomainException(
                "Discord is not configured for Dorks & Dice.");
        }

        var activated = await identityDbContext.AccountLinkModeActivations.AnyAsync(
            activation =>
                activation.UserId == ownerUserId
                && activation.ModeId == ModeId
                && activation.ProviderId == DiscordProvider.Id
                && activation.ResourceId == modeConnection.ResourceId,
            cancellationToken);
        if (!activated)
        {
            throw new CampaignDomainException(
                "Connect and enable your Discord account for Dorks & Dice before configuring Discord servers.");
        }

        if (string.Equals(
                modeConnection.ResourceId,
                normalizedGuildId,
                StringComparison.Ordinal))
        {
            throw new CampaignDomainException(
                "The main Dorks & Dice Discord server is managed by the mode and can not be added as a user-managed server.");
        }

        var ownership = await ownershipVerifier.VerifyAsync(
            ownerUserId,
            normalizedGuildId,
            cancellationToken);
        if (!ownership.IsVerified)
        {
            throw new CampaignDomainException(OwnershipError(ownership.Status));
        }

        var duplicateOwner = await dbContext.DiscordServerBindings
            .AsNoTracking()
            .Where(binding =>
                binding.GuildId == normalizedGuildId
                && (!bindingId.HasValue || binding.Id != bindingId.Value))
            .Select(binding => (Guid?)binding.OwnerUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (duplicateOwner is not null)
        {
            throw new CampaignDomainException(
                duplicateOwner == ownerUserId
                    ? "That Discord server is already configured on your account."
                    : "That Discord server is already configured by another Dorks & Dice account.");
        }

        var requestedCampaignIds = campaignIds
            .Distinct()
            .ToArray();
        var eligibleCampaignIds = (await GetEligibleCampaignsAsync(
                ownerUserId,
                cancellationToken))
            .Select(campaign => campaign.Id)
            .ToHashSet();

        switch (scope)
        {
            case DiscordServerCampaignScope.SingleCampaign:
                if (requestedCampaignIds.Length != 1)
                {
                    throw new CampaignDomainException(
                        "Single-campaign Discord servers require exactly one campaign.");
                }
                break;
            case DiscordServerCampaignScope.SelectedCampaigns:
                if (requestedCampaignIds.Length == 0)
                {
                    throw new CampaignDomainException(
                        "Select at least one campaign for this Discord server.");
                }
                break;
            case DiscordServerCampaignScope.AllDmCampaigns:
                requestedCampaignIds = [];
                break;
        }

        if (requestedCampaignIds.Any(id => !eligibleCampaignIds.Contains(id)))
        {
            throw new CampaignDomainException(
                "You can only assign Discord servers to active campaigns where you are currently a DM.");
        }

        DorksAndDiceDiscordServerBinding binding;
        if (bindingId is Guid existingId)
        {
            binding = await dbContext.DiscordServerBindings
                .Include(item => item.Campaigns)
                .SingleOrDefaultAsync(
                    item => item.Id == existingId
                        && item.OwnerUserId == ownerUserId,
                    cancellationToken)
                ?? throw new CampaignDomainException(
                    "Discord server configuration does not exist.");
        }
        else
        {
            binding = new DorksAndDiceDiscordServerBinding
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                CreatedAt = timeProvider.GetUtcNow()
            };
            dbContext.DiscordServerBindings.Add(binding);
        }

        binding.GuildId = normalizedGuildId;
        binding.CampaignScope = scope;
        binding.UpdatedAt = timeProvider.GetUtcNow();

        dbContext.DiscordServerCampaigns.RemoveRange(binding.Campaigns);
        binding.Campaigns.Clear();

        foreach (var campaignId in requestedCampaignIds)
        {
            binding.Campaigns.Add(new DorksAndDiceDiscordServerCampaign
            {
                BindingId = binding.Id,
                CampaignId = campaignId,
                Binding = binding
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return binding;
    }

    public async Task RemoveAsync(
        Guid ownerUserId,
        Guid bindingId,
        CancellationToken cancellationToken = default)
    {
        var binding = await dbContext.DiscordServerBindings
            .SingleOrDefaultAsync(
                item => item.Id == bindingId
                    && item.OwnerUserId == ownerUserId,
                cancellationToken);
        if (binding is null)
        {
            return;
        }

        dbContext.DiscordServerBindings.Remove(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeGuildId(string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
        {
            throw new CampaignDomainException("Discord server ID is required.");
        }

        var normalized = guildId.Trim();
        if (normalized.Length > DorksAndDiceDiscordServerBinding.GuildIdMaxLength
            || normalized.Any(character => character is < '0' or > '9'))
        {
            throw new CampaignDomainException(
                "Discord server ID must contain only digits.");
        }

        return normalized;
    }

    private static void ValidateScope(DiscordServerCampaignScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new CampaignDomainException(
                "Discord server campaign scope is invalid.");
        }
    }

    private static string OwnershipError(DiscordGuildOwnershipStatus status) => status switch
    {
        DiscordGuildOwnershipStatus.DiscordAccountNotLinked =>
            "A linked Discord account is required to configure a Discord server.",
        DiscordGuildOwnershipStatus.BotUnavailable =>
            "The Discord bot is not enabled on this Site deployment.",
        DiscordGuildOwnershipStatus.BotNotInstalled =>
            "Install the Dorks & Dice bot in that Discord server before configuring it here.",
        DiscordGuildOwnershipStatus.NotOwner =>
            "The linked Discord account is not the owner of that Discord server.",
        _ => "The Discord server could not be verified."
    };
}
