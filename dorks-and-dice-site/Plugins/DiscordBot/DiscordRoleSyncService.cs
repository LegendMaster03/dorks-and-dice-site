using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public interface IDiscordWorkspaceSyncService
{
    Task SynchronizeAsync(CancellationToken cancellationToken = default);
}

public sealed class DiscordWorkspaceSyncService(
    IdentityDbContext identityDbContext,
    IEnumerable<IDiscordWorkspaceProjectionSource> projectionSources,
    IDiscordBotClient discord,
    TimeProvider timeProvider,
    ILogger<DiscordWorkspaceSyncService> logger) : IDiscordWorkspaceSyncService
{
    private readonly IdentityDbContext _identityDbContext = identityDbContext;
    private readonly IReadOnlyList<IDiscordWorkspaceProjectionSource> _projectionSources =
        projectionSources.ToArray();
    private readonly IDiscordBotClient _discord = discord;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DiscordWorkspaceSyncService> _logger = logger;

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var source in _projectionSources)
        {
            try
            {
                var projections = await source.BuildAsync(cancellationToken);
                Validate(source.SourceId, projections);
                await ReconcileSourceAsync(source.SourceId, projections, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Discord workspace projection {SourceId} failed.",
                    source.SourceId);
            }
        }
    }

    private async Task ReconcileSourceAsync(
        string sourceId,
        IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections,
        CancellationToken cancellationToken)
    {
        await ReconcileRolesAsync(sourceId, projections, cancellationToken);
        await ReconcileChannelsAsync(sourceId, projections, cancellationToken);
    }

    private async Task ReconcileRolesAsync(
        string sourceId,
        IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections,
        CancellationToken cancellationToken)
    {
        var desired = projections
            .SelectMany(projection => projection.Roles.Select(role => new DesiredRole(
                projection.ModeId,
                projection.ActivationResourceId,
                projection.GuildId,
                role)))
            .ToDictionary(
                item => RoleIdentity(item.GuildId, item.Role.Key),
                StringComparer.Ordinal);

        var existingMappings = await _identityDbContext.DiscordManagedRoles
            .Where(role => role.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        foreach (var stale in existingMappings
            .Where(mapping => !desired.ContainsKey(RoleIdentity(mapping.GuildId, mapping.RoleKey)))
            .ToArray())
        {
            if (!await RemoveManagedRoleAsync(stale, cancellationToken))
            {
                continue;
            }

            existingMappings.Remove(stale);
        }

        var guildRoles = new Dictionary<string, IReadOnlyDictionary<string, DiscordGuildRole>>(
            StringComparer.Ordinal);

        foreach (var desiredRole in desired.Values)
        {
            if (!guildRoles.TryGetValue(desiredRole.GuildId, out var rolesById))
            {
                var roles = await _discord.GetGuildRolesAsync(
                    desiredRole.GuildId,
                    cancellationToken);
                rolesById = roles.ToDictionary(role => role.Id, StringComparer.Ordinal);
                guildRoles[desiredRole.GuildId] = rolesById;
            }

            var mapping = existingMappings.SingleOrDefault(candidate =>
                candidate.GuildId == desiredRole.GuildId
                && candidate.RoleKey == desiredRole.Role.Key);

            if (mapping is null
                || !rolesById.TryGetValue(mapping.DiscordRoleId, out var actualRole))
            {
                if (mapping is not null)
                {
                    var orphanAssignments = await _identityDbContext.DiscordManagedRoleAssignments
                        .Where(assignment =>
                            assignment.SourceId == sourceId
                            && assignment.GuildId == mapping.GuildId
                            && assignment.RoleKey == mapping.RoleKey)
                        .ToListAsync(cancellationToken);
                    _identityDbContext.DiscordManagedRoleAssignments.RemoveRange(orphanAssignments);
                    _identityDbContext.DiscordManagedRoles.Remove(mapping);
                    existingMappings.Remove(mapping);
                    await _identityDbContext.SaveChangesAsync(cancellationToken);
                }

                var created = await _discord.CreateGuildRoleAsync(
                    desiredRole.GuildId,
                    desiredRole.Role.DisplayName,
                    cancellationToken);
                mapping = new DiscordManagedRole
                {
                    SourceId = sourceId,
                    GuildId = desiredRole.GuildId,
                    RoleKey = desiredRole.Role.Key,
                    DiscordRoleId = created.Id,
                    DisplayName = desiredRole.Role.DisplayName,
                    UpdatedAt = _timeProvider.GetUtcNow()
                };
                _identityDbContext.DiscordManagedRoles.Add(mapping);
                existingMappings.Add(mapping);
                await _identityDbContext.SaveChangesAsync(cancellationToken);
            }
            else if (!string.Equals(
                         actualRole.Name,
                         desiredRole.Role.DisplayName,
                         StringComparison.Ordinal))
            {
                await _discord.UpdateGuildRoleAsync(
                    desiredRole.GuildId,
                    mapping.DiscordRoleId,
                    desiredRole.Role.DisplayName,
                    cancellationToken);
                mapping.DisplayName = desiredRole.Role.DisplayName;
                mapping.UpdatedAt = _timeProvider.GetUtcNow();
                await _identityDbContext.SaveChangesAsync(cancellationToken);
            }

            await ReconcileAssignmentsAsync(
                sourceId,
                desiredRole,
                mapping,
                cancellationToken);
        }
    }

    private async Task ReconcileChannelsAsync(
        string sourceId,
        IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections,
        CancellationToken cancellationToken)
    {
        var desired = projections
            .SelectMany(projection => projection.Channels.Select(channel => new DesiredChannel(
                projection.GuildId,
                channel)))
            .ToDictionary(
                item => ChannelIdentity(item.GuildId, item.Channel.Key),
                StringComparer.Ordinal);

        var existingMappings = await _identityDbContext.DiscordManagedChannels
            .Where(channel => channel.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        foreach (var stale in existingMappings
            .Where(mapping =>
                !desired.ContainsKey(ChannelIdentity(mapping.GuildId, mapping.ChannelKey)))
            .OrderBy(mapping =>
                mapping.Kind == (int)DiscordManagedChannelKind.Category ? 1 : 0)
            .ToArray())
        {
            if (!await RemoveManagedChannelAsync(stale, cancellationToken))
            {
                continue;
            }

            existingMappings.Remove(stale);
        }

        var guildChannels = new Dictionary<string, IReadOnlyDictionary<string, DiscordGuildChannel>>(
            StringComparer.Ordinal);

        foreach (var desiredChannel in desired.Values
            .OrderBy(item =>
                item.Channel.Kind == DiscordManagedChannelKind.Category ? 0 : 1)
            .ThenBy(item => item.Channel.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!guildChannels.TryGetValue(desiredChannel.GuildId, out var channelsById))
            {
                var channels = await _discord.GetGuildChannelsAsync(
                    desiredChannel.GuildId,
                    cancellationToken);
                channelsById = channels.ToDictionary(channel => channel.Id, StringComparer.Ordinal);
                guildChannels[desiredChannel.GuildId] = channelsById;
            }

            var mapping = existingMappings.SingleOrDefault(candidate =>
                candidate.GuildId == desiredChannel.GuildId
                && candidate.ChannelKey == desiredChannel.Channel.Key);

            if (mapping is not null
                && (!channelsById.TryGetValue(mapping.DiscordChannelId, out var actual)
                    || actual.Kind != desiredChannel.Channel.Kind))
            {
                if (channelsById.ContainsKey(mapping.DiscordChannelId))
                {
                    await _discord.DeleteGuildChannelAsync(
                        mapping.DiscordChannelId,
                        cancellationToken);
                }

                _identityDbContext.DiscordManagedChannels.Remove(mapping);
                existingMappings.Remove(mapping);
                await _identityDbContext.SaveChangesAsync(cancellationToken);
                mapping = null;
            }

            var parentId = ResolveParentId(
                sourceId,
                desiredChannel,
                existingMappings);

            if (mapping is null)
            {
                var created = await _discord.CreateGuildChannelAsync(
                    desiredChannel.GuildId,
                    desiredChannel.Channel.Name,
                    desiredChannel.Channel.Kind,
                    parentId,
                    cancellationToken);

                mapping = new DiscordManagedChannel
                {
                    SourceId = sourceId,
                    GuildId = desiredChannel.GuildId,
                    ChannelKey = desiredChannel.Channel.Key,
                    DiscordChannelId = created.Id,
                    Name = desiredChannel.Channel.Name,
                    Kind = (int)desiredChannel.Channel.Kind,
                    ParentKey = desiredChannel.Channel.ParentKey,
                    UpdatedAt = _timeProvider.GetUtcNow()
                };
                _identityDbContext.DiscordManagedChannels.Add(mapping);
                existingMappings.Add(mapping);
                await _identityDbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            var current = channelsById[mapping.DiscordChannelId];
            if (!string.Equals(
                    current.Name,
                    desiredChannel.Channel.Name,
                    StringComparison.Ordinal)
                || !string.Equals(current.ParentId, parentId, StringComparison.Ordinal))
            {
                await _discord.UpdateGuildChannelAsync(
                    mapping.DiscordChannelId,
                    desiredChannel.Channel.Name,
                    parentId,
                    cancellationToken);
            }

            mapping.Name = desiredChannel.Channel.Name;
            mapping.ParentKey = desiredChannel.Channel.ParentKey;
            mapping.UpdatedAt = _timeProvider.GetUtcNow();
            await _identityDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private string? ResolveParentId(
        string sourceId,
        DesiredChannel desired,
        IReadOnlyCollection<DiscordManagedChannel> mappings)
    {
        if (string.IsNullOrWhiteSpace(desired.Channel.ParentKey))
        {
            return null;
        }

        var parent = mappings.SingleOrDefault(mapping =>
            mapping.SourceId == sourceId
            && mapping.GuildId == desired.GuildId
            && mapping.ChannelKey == desired.Channel.ParentKey);
        if (parent is null)
        {
            throw new InvalidOperationException(
                $"Discord channel '{desired.Channel.Key}' references parent '{desired.Channel.ParentKey}' before it is available.");
        }

        return parent.DiscordChannelId;
    }

    private async Task ReconcileAssignmentsAsync(
        string sourceId,
        DesiredRole desired,
        DiscordManagedRole mapping,
        CancellationToken cancellationToken)
    {
        var requestedUsers = desired.Role.UserIds.Distinct().ToArray();
        var activeUsers = await _identityDbContext.AccountLinkModeActivations
            .Where(activation =>
                activation.ModeId == desired.ModeId
                && activation.ProviderId == DiscordProvider.Id
                && activation.ResourceId == desired.ActivationResourceId
                && requestedUsers.Contains(activation.UserId))
            .Select(activation => activation.UserId)
            .ToArrayAsync(cancellationToken);

        var linkedUsers = await _identityDbContext.UserLogins
            .Where(login =>
                login.LoginProvider == DiscordProvider.Id
                && activeUsers.Contains(login.UserId))
            .Select(login => new { login.UserId, login.ProviderKey })
            .ToListAsync(cancellationToken);

        var desiredDiscordUsers = linkedUsers
            .Select(login => login.ProviderKey)
            .Where(providerKey => !string.IsNullOrWhiteSpace(providerKey))
            .ToHashSet(StringComparer.Ordinal);

        var assignments = await _identityDbContext.DiscordManagedRoleAssignments
            .Where(assignment =>
                assignment.SourceId == sourceId
                && assignment.GuildId == desired.GuildId
                && assignment.RoleKey == desired.Role.Key)
            .ToListAsync(cancellationToken);

        foreach (var stale in assignments
            .Where(assignment =>
                assignment.DiscordRoleId != mapping.DiscordRoleId
                || !desiredDiscordUsers.Contains(assignment.DiscordUserId))
            .ToArray())
        {
            await _discord.RemoveMemberRoleAsync(
                stale.GuildId,
                stale.DiscordUserId,
                stale.DiscordRoleId,
                cancellationToken);
            _identityDbContext.DiscordManagedRoleAssignments.Remove(stale);
            assignments.Remove(stale);
        }

        var assignedUsers = assignments
            .Where(assignment => assignment.DiscordRoleId == mapping.DiscordRoleId)
            .Select(assignment => assignment.DiscordUserId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var discordUserId in desiredDiscordUsers.Except(assignedUsers))
        {
            var added = await _discord.AddMemberRoleAsync(
                desired.GuildId,
                discordUserId,
                mapping.DiscordRoleId,
                cancellationToken);
            if (!added)
            {
                continue;
            }

            _identityDbContext.DiscordManagedRoleAssignments.Add(
                new DiscordManagedRoleAssignment
                {
                    SourceId = sourceId,
                    GuildId = desired.GuildId,
                    RoleKey = desired.Role.Key,
                    DiscordUserId = discordUserId,
                    DiscordRoleId = mapping.DiscordRoleId,
                    UpdatedAt = _timeProvider.GetUtcNow()
                });
        }

        await _identityDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> RemoveManagedRoleAsync(
        DiscordManagedRole mapping,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignments = await _identityDbContext.DiscordManagedRoleAssignments
                .Where(assignment =>
                    assignment.SourceId == mapping.SourceId
                    && assignment.GuildId == mapping.GuildId
                    && assignment.RoleKey == mapping.RoleKey)
                .ToListAsync(cancellationToken);

            foreach (var assignment in assignments)
            {
                await _discord.RemoveMemberRoleAsync(
                    assignment.GuildId,
                    assignment.DiscordUserId,
                    assignment.DiscordRoleId,
                    cancellationToken);
            }

            await _discord.DeleteGuildRoleAsync(
                mapping.GuildId,
                mapping.DiscordRoleId,
                cancellationToken);

            _identityDbContext.DiscordManagedRoleAssignments.RemoveRange(assignments);
            _identityDbContext.DiscordManagedRoles.Remove(mapping);
            await _identityDbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Could not remove managed Discord role {RoleKey} from guild {GuildId}.",
                mapping.RoleKey,
                mapping.GuildId);
            return false;
        }
    }

    private async Task<bool> RemoveManagedChannelAsync(
        DiscordManagedChannel mapping,
        CancellationToken cancellationToken)
    {
        try
        {
            await _discord.DeleteGuildChannelAsync(
                mapping.DiscordChannelId,
                cancellationToken);
            _identityDbContext.DiscordManagedChannels.Remove(mapping);
            await _identityDbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Could not remove managed Discord channel {ChannelKey} from guild {GuildId}.",
                mapping.ChannelKey,
                mapping.GuildId);
            return false;
        }
    }

    private static void Validate(
        string sourceId,
        IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        var seenRoles = new HashSet<string>(StringComparer.Ordinal);
        var seenChannels = new HashSet<string>(StringComparer.Ordinal);

        foreach (var projection in projections)
        {
            ValidateSnowflake(projection.GuildId, "guild");
            ValidateSnowflake(projection.ActivationResourceId, "activation resource");

            foreach (var role in projection.Roles)
            {
                if (string.IsNullOrWhiteSpace(role.Key)
                    || role.Key.Length > DiscordManagedRole.RoleKeyMaxLength)
                {
                    throw new InvalidOperationException(
                        $"Discord workspace projection '{sourceId}' contains an invalid role key.");
                }

                if (string.IsNullOrWhiteSpace(role.DisplayName)
                    || role.DisplayName.Length > DiscordManagedRole.DisplayNameMaxLength)
                {
                    throw new InvalidOperationException(
                        $"Discord role '{role.Key}' has an invalid display name.");
                }

                if (!seenRoles.Add(RoleIdentity(projection.GuildId, role.Key)))
                {
                    throw new InvalidOperationException(
                        $"Discord workspace projection '{sourceId}' emitted duplicate role '{role.Key}' for guild '{projection.GuildId}'.");
                }
            }

            var channelsByKey = projection.Channels.ToDictionary(
                channel => channel.Key,
                StringComparer.Ordinal);

            foreach (var channel in projection.Channels)
            {
                if (string.IsNullOrWhiteSpace(channel.Key)
                    || channel.Key.Length > DiscordManagedChannel.ChannelKeyMaxLength)
                {
                    throw new InvalidOperationException(
                        $"Discord workspace projection '{sourceId}' contains an invalid channel key.");
                }

                if (string.IsNullOrWhiteSpace(channel.Name)
                    || channel.Name.Length > DiscordManagedChannel.NameMaxLength)
                {
                    throw new InvalidOperationException(
                        $"Discord channel '{channel.Key}' has an invalid name.");
                }

                if (!seenChannels.Add(ChannelIdentity(projection.GuildId, channel.Key)))
                {
                    throw new InvalidOperationException(
                        $"Discord workspace projection '{sourceId}' emitted duplicate channel '{channel.Key}' for guild '{projection.GuildId}'.");
                }

                if (!string.IsNullOrWhiteSpace(channel.ParentKey))
                {
                    if (!channelsByKey.TryGetValue(channel.ParentKey, out var parent)
                        || parent.Kind != DiscordManagedChannelKind.Category)
                    {
                        throw new InvalidOperationException(
                            $"Discord channel '{channel.Key}' must reference a desired category in the same guild.");
                    }

                    if (channel.Kind == DiscordManagedChannelKind.Category)
                    {
                        throw new InvalidOperationException(
                            $"Discord category '{channel.Key}' can not have a parent.");
                    }
                }
            }
        }
    }

    private static void ValidateSnowflake(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > DiscordManagedRole.GuildIdMaxLength
            || value.Any(character => character is < '0' or > '9'))
        {
            throw new InvalidOperationException(
                $"Discord {label} ID '{value}' is invalid.");
        }
    }

    private static string RoleIdentity(string guildId, string roleKey) =>
        $"{guildId}\u001f{roleKey}";

    private static string ChannelIdentity(string guildId, string channelKey) =>
        $"{guildId}\u001f{channelKey}";

    private sealed record DesiredRole(
        string ModeId,
        string ActivationResourceId,
        string GuildId,
        DiscordDesiredRole Role);

    private sealed record DesiredChannel(
        string GuildId,
        DiscordDesiredChannel Channel);
}

public sealed class DiscordWorkspaceSyncWorker(
    IServiceScopeFactory scopeFactory,
    DiscordBotOptions options,
    ILogger<DiscordWorkspaceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, options.ReconcileIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var synchronizer = scope.ServiceProvider
                    .GetRequiredService<IDiscordWorkspaceSyncService>();
                await synchronizer.SynchronizeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Discord workspace synchronization failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
