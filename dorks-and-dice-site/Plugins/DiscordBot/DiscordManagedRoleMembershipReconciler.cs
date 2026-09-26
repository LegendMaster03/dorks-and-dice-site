using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public sealed class DiscordManagedRoleMembershipReconciler(
    IdentityDbContext identityDbContext,
    IEnumerable<IDiscordWorkspaceProjectionSource> projectionSources,
    IDiscordBotClient discord,
    ILogger<DiscordManagedRoleMembershipReconciler> logger)
{
    private readonly IdentityDbContext _identityDbContext = identityDbContext;
    private readonly IReadOnlyList<IDiscordWorkspaceProjectionSource> _projectionSources = projectionSources.ToArray();
    private readonly IDiscordBotClient _discord = discord;
    private readonly ILogger<DiscordManagedRoleMembershipReconciler> _logger = logger;

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var membersByGuild = new Dictionary<string, IReadOnlyList<DiscordGuildMember>>(StringComparer.Ordinal);

        foreach (var source in _projectionSources)
        {
            IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections;
            try
            {
                projections = await source.BuildAsync(cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Could not build Discord projection {SourceId} for membership reconciliation.", source.SourceId);
                continue;
            }

            var mappings = await _identityDbContext.DiscordManagedRoles
                .AsNoTracking()
                .Where(mapping => mapping.SourceId == source.SourceId)
                .ToListAsync(cancellationToken);

            foreach (var projection in projections)
            {
                if (!membersByGuild.TryGetValue(projection.GuildId, out var members))
                {
                    members = await _discord.GetGuildMembersAsync(projection.GuildId, cancellationToken);
                    membersByGuild[projection.GuildId] = members;
                }

                foreach (var role in projection.Roles)
                {
                    var mapping = mappings.SingleOrDefault(candidate =>
                        candidate.GuildId == projection.GuildId
                        && candidate.RoleKey == role.Key);
                    if (mapping is null)
                    {
                        continue;
                    }

                    var requestedUsers = role.UserIds.Distinct().ToArray();
                    var activeUsers = await _identityDbContext.AccountLinkModeActivations
                        .Where(activation =>
                            activation.ModeId == projection.ModeId
                            && activation.ProviderId == DiscordProvider.Id
                            && activation.ResourceId == projection.ActivationResourceId
                            && requestedUsers.Contains(activation.UserId))
                        .Select(activation => activation.UserId)
                        .ToArrayAsync(cancellationToken);

                    var desiredDiscordUserIds = await _identityDbContext.UserLogins
                        .Where(login =>
                            login.LoginProvider == DiscordProvider.Id
                            && activeUsers.Contains(login.UserId))
                        .Select(login => login.ProviderKey)
                        .Where(providerKey => providerKey != string.Empty)
                        .ToArrayAsync(cancellationToken);
                    var desiredDiscordUsers = desiredDiscordUserIds.ToHashSet(StringComparer.Ordinal);

                    var actualDiscordUsers = members
                        .Where(member => member.RoleIds.Contains(mapping.DiscordRoleId, StringComparer.Ordinal))
                        .Select(member => member.UserId)
                        .ToHashSet(StringComparer.Ordinal);

                    foreach (var discordUserId in actualDiscordUsers.Except(desiredDiscordUsers))
                    {
                        await _discord.RemoveMemberRoleAsync(
                            projection.GuildId,
                            discordUserId,
                            mapping.DiscordRoleId,
                            cancellationToken);
                    }
                }
            }
        }
    }
}

public sealed class AuthoritativeDiscordWorkspaceSyncService(
    DiscordWorkspaceSyncService workspaceSync,
    DiscordManagedRoleMembershipReconciler roleMembershipReconciler) : IDiscordWorkspaceSyncService
{
    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await workspaceSync.SynchronizeAsync(cancellationToken);
        await roleMembershipReconciler.ReconcileAsync(cancellationToken);
    }
}