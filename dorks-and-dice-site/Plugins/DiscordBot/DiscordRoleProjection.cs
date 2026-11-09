namespace dorks_and_dice_site.Plugins.DiscordBot;

public enum DiscordManagedChannelKind
{
    Text = 0,
    Voice = 2,
    Category = 4
}

public sealed record DiscordDesiredRole(
    string Key,
    string DisplayName,
    IReadOnlyCollection<Guid> UserIds);

public sealed record DiscordDesiredChannel(
    string Key,
    string Name,
    DiscordManagedChannelKind Kind,
    string? ParentKey = null);

public sealed record DiscordGuildWorkspaceProjection(
    string ModeId,
    string ActivationResourceId,
    string GuildId,
    IReadOnlyCollection<DiscordDesiredRole> Roles,
    IReadOnlyCollection<DiscordDesiredChannel> Channels);

/// <summary>
/// Mode-owned contribution of desired Discord workspace roles/channels.
/// SourceId identifies the contributor independently from a shared guild.
/// </summary>
public interface IDiscordWorkspaceProjectionSource
{
    string SourceId { get; }

    Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
        CancellationToken cancellationToken = default);
}
