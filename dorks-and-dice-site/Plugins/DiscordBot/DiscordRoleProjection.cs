namespace dorks_and_dice_site.Plugins.DiscordBot;

public sealed record DiscordDesiredRole(
    string Key,
    string DisplayName,
    IReadOnlyCollection<Guid> UserIds);

public sealed record DiscordGuildRoleProjection(
    string ModeId,
    string ActivationResourceId,
    string GuildId,
    IReadOnlyCollection<DiscordDesiredRole> Roles);

public interface IDiscordRoleProjectionSource
{
    string SourceId { get; }

    Task<IReadOnlyCollection<DiscordGuildRoleProjection>> BuildAsync(
        CancellationToken cancellationToken = default);
}
