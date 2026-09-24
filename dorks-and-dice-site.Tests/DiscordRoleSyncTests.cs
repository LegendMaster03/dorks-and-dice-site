using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dorks_and_dice_site.Tests;

public sealed class DiscordRoleSyncTests
{
    [Fact]
    public async Task ReconciliationCreatesAssignsAndCleansOnlyManagedRole()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "discord-sync@example.test",
            NormalizedUserName = "DISCORD-SYNC@EXAMPLE.TEST",
            Email = "discord-sync@example.test",
            NormalizedEmail = "DISCORD-SYNC@EXAMPLE.TEST",
            DisplayName = "Discord Sync",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>
        {
            LoginProvider = DiscordProvider.Id,
            ProviderKey = "777",
            ProviderDisplayName = "Discord",
            UserId = userId
        });
        db.AccountLinkModeActivations.Add(new AccountLinkModeActivation
        {
            UserId = userId,
            ModeId = "dorks-and-dice",
            ProviderId = DiscordProvider.Id,
            ResourceId = "100",
            ActivatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var source = new MutableProjectionSource
        {
            Projections =
            [
                new DiscordGuildRoleProjection(
                    "dorks-and-dice",
                    "100",
                    "100",
                    [
                        new DiscordDesiredRole(
                            "general:player",
                            "Player",
                            [userId])
                    ])
            ]
        };
        var client = new FakeDiscordBotClient();
        var sync = new DiscordRoleSyncService(
            db,
            [source],
            client,
            TimeProvider.System,
            NullLogger<DiscordRoleSyncService>.Instance);

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Equal(("100", "Player"), client.CreatedRoles[0]);
        Assert.Single(client.AddedRoles);
        Assert.Equal("777", client.AddedRoles[0].UserId);
        Assert.Single(await db.DiscordManagedRoles.ToListAsync());
        Assert.Single(await db.DiscordManagedRoleAssignments.ToListAsync());

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Single(client.AddedRoles);

        source.Projections =
        [
            new DiscordGuildRoleProjection(
                "dorks-and-dice",
                "100",
                "100",
                [])
        ];

        await sync.SynchronizeAsync();

        Assert.Single(client.RemovedRoles);
        Assert.Single(client.DeletedRoles);
        Assert.Empty(await db.DiscordManagedRoles.ToListAsync());
        Assert.Empty(await db.DiscordManagedRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task UsersWithoutModeActivationAreNotAssigned()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "linked-only@example.test",
            NormalizedUserName = "LINKED-ONLY@EXAMPLE.TEST",
            Email = "linked-only@example.test",
            NormalizedEmail = "LINKED-ONLY@EXAMPLE.TEST",
            DisplayName = "Linked Only",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>
        {
            LoginProvider = DiscordProvider.Id,
            ProviderKey = "888",
            ProviderDisplayName = "Discord",
            UserId = userId
        });
        await db.SaveChangesAsync();

        var source = new MutableProjectionSource
        {
            Projections =
            [
                new DiscordGuildRoleProjection(
                    "dorks-and-dice",
                    "100",
                    "100",
                    [
                        new DiscordDesiredRole(
                            "general:dm",
                            "DM",
                            [userId])
                    ])
            ]
        };
        var client = new FakeDiscordBotClient();
        var sync = new DiscordRoleSyncService(
            db,
            [source],
            client,
            TimeProvider.System,
            NullLogger<DiscordRoleSyncService>.Instance);

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Empty(client.AddedRoles);
        Assert.Empty(await db.DiscordManagedRoleAssignments.ToListAsync());
    }

    private sealed class MutableProjectionSource : IDiscordRoleProjectionSource
    {
        public string SourceId => "test-source";

        public IReadOnlyCollection<DiscordGuildRoleProjection> Projections { get; set; } = [];

        public Task<IReadOnlyCollection<DiscordGuildRoleProjection>> BuildAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Projections);
    }

    private sealed class FakeDiscordBotClient : IDiscordBotClient
    {
        private int _nextRoleId = 900;

        public List<(string GuildId, string Name)> CreatedRoles { get; } = [];
        public List<(string GuildId, string RoleId, string Name)> UpdatedRoles { get; } = [];
        public List<(string GuildId, string RoleId)> DeletedRoles { get; } = [];
        public List<(string GuildId, string UserId, string RoleId)> AddedRoles { get; } = [];
        public List<(string GuildId, string UserId, string RoleId)> RemovedRoles { get; } = [];
        private readonly Dictionary<string, List<DiscordGuildRole>> _roles = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<DiscordGuildRole>> GetGuildRolesAsync(
            string guildId,
            CancellationToken cancellationToken = default)
        {
            if (!_roles.TryGetValue(guildId, out var roles))
            {
                roles = [];
                _roles[guildId] = roles;
            }

            return Task.FromResult<IReadOnlyList<DiscordGuildRole>>(roles.ToArray());
        }

        public Task<DiscordGuildRole> CreateGuildRoleAsync(
            string guildId,
            string name,
            CancellationToken cancellationToken = default)
        {
            var role = new DiscordGuildRole((++_nextRoleId).ToString(), name);
            if (!_roles.TryGetValue(guildId, out var roles))
            {
                roles = [];
                _roles[guildId] = roles;
            }

            roles.Add(role);
            CreatedRoles.Add((guildId, name));
            return Task.FromResult(role);
        }

        public Task UpdateGuildRoleAsync(
            string guildId,
            string roleId,
            string name,
            CancellationToken cancellationToken = default)
        {
            var roles = _roles[guildId];
            var index = roles.FindIndex(role => role.Id == roleId);
            roles[index] = new DiscordGuildRole(roleId, name);
            UpdatedRoles.Add((guildId, roleId, name));
            return Task.CompletedTask;
        }

        public Task DeleteGuildRoleAsync(
            string guildId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            if (_roles.TryGetValue(guildId, out var roles))
            {
                roles.RemoveAll(role => role.Id == roleId);
            }

            DeletedRoles.Add((guildId, roleId));
            return Task.CompletedTask;
        }

        public Task<bool> AddMemberRoleAsync(
            string guildId,
            string userId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            AddedRoles.Add((guildId, userId, roleId));
            return Task.FromResult(true);
        }

        public Task RemoveMemberRoleAsync(
            string guildId,
            string userId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            RemovedRoles.Add((guildId, userId, roleId));
            return Task.CompletedTask;
        }
    }
}
