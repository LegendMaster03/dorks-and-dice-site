using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public interface IDiscordRoleSyncService
{
    Task SynchronizeAsync(CancellationToken cancellationToken = default);
}

public sealed class DiscordRoleSyncService(
    IdentityDbContext identityDbContext,
    IEnumerable<IDiscordRoleProjectionSource> projectionSources,
    IDiscordBotClient discord,
    TimeProvider timeProvider,
    ILogger<DiscordRoleSyncService> logger) : IDiscordRoleSyncService
{
    private readonly IdentityDbContext _identityDbContext = identityDbContext;
    private readonly IReadOnlyList<IDiscordRoleProjectionSource> _projectionSources =
        projectionSources.ToArray();
    private readonly IDiscordBotClient _discord = discord;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DiscordRoleSyncService> _logger = logger;

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
                    "Discord role projection {SourceId} failed.",
                    source.SourceId);
            }
        }
    }

    private async Task ReconcileSourceAsync(
        string sourceId,
        IReadOnlyCollection<DiscordGuildRoleProjection> projections,
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

    private static void Validate(
        string sourceId,
        IReadOnlyCollection<DiscordGuildRoleProjection> projections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        var seen = new HashSet<string>(StringComparer.Ordinal);

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
                        $"Discord role projection '{sourceId}' contains an invalid role key.");
                }

                if (string.IsNullOrWhiteSpace(role.DisplayName)
                    || role.DisplayName.Length > DiscordManagedRole.DisplayNameMaxLength)
                {
                    throw new InvalidOperationException(
                        $"Discord role '{role.Key}' has an invalid display name.");
                }

                if (!seen.Add(RoleIdentity(projection.GuildId, role.Key)))
                {
                    throw new InvalidOperationException(
                        $"Discord role projection '{sourceId}' emitted duplicate role '{role.Key}' for guild '{projection.GuildId}'.");
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

    private sealed record DesiredRole(
        string ModeId,
        string ActivationResourceId,
        string GuildId,
        DiscordDesiredRole Role);
}

public sealed class DiscordRoleSyncWorker(
    IServiceScopeFactory scopeFactory,
    DiscordBotOptions options,
    ILogger<DiscordRoleSyncWorker> logger) : BackgroundService
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
                    .GetRequiredService<IDiscordRoleSyncService>();
                await synchronizer.SynchronizeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Discord role synchronization failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
