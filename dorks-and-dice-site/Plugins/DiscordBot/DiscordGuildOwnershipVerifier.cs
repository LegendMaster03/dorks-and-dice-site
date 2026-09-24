using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public enum DiscordGuildOwnershipStatus
{
    Verified,
    DiscordAccountNotLinked,
    BotUnavailable,
    BotNotInstalled,
    NotOwner
}

public sealed record DiscordGuildOwnershipResult(
    DiscordGuildOwnershipStatus Status,
    string? GuildName = null)
{
    public bool IsVerified => Status == DiscordGuildOwnershipStatus.Verified;
}

public interface IDiscordGuildOwnershipVerifier
{
    Task<DiscordGuildOwnershipResult> VerifyAsync(
        Guid siteUserId,
        string guildId,
        CancellationToken cancellationToken = default);
}

public sealed class DiscordGuildOwnershipVerifier(
    IdentityDbContext identityDbContext,
    IDiscordBotClient discord) : IDiscordGuildOwnershipVerifier
{
    public async Task<DiscordGuildOwnershipResult> VerifyAsync(
        Guid siteUserId,
        string guildId,
        CancellationToken cancellationToken = default)
    {
        var discordUserId = await identityDbContext.UserLogins
            .Where(login =>
                login.UserId == siteUserId
                && login.LoginProvider == DiscordProvider.Id)
            .Select(login => login.ProviderKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(discordUserId))
        {
            return new(DiscordGuildOwnershipStatus.DiscordAccountNotLinked);
        }

        var guild = await discord.GetGuildAsync(guildId, cancellationToken);
        if (guild is null)
        {
            return new(DiscordGuildOwnershipStatus.BotNotInstalled);
        }

        return string.Equals(guild.OwnerId, discordUserId, StringComparison.Ordinal)
            ? new(DiscordGuildOwnershipStatus.Verified, guild.Name)
            : new(DiscordGuildOwnershipStatus.NotOwner, guild.Name);
    }
}

public sealed class DiscordGuildOwnershipVerifierUnavailable
    : IDiscordGuildOwnershipVerifier
{
    public Task<DiscordGuildOwnershipResult> VerifyAsync(
        Guid siteUserId,
        string guildId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new DiscordGuildOwnershipResult(
                DiscordGuildOwnershipStatus.BotUnavailable));
}
