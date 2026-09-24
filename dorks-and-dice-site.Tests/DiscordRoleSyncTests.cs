using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dorks_and_dice_site.Tests;

public sealed class DiscordWorkspaceSyncTests
{
    [Fact]
    public async Task ReconciliationCreatesAssignsAndCleansOnlyManagedWorkspaceState()
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
                new DiscordGuildWorkspaceProjection(
                    "dorks-and-dice",
                    "100",
                    "100",
                    [
                        new DiscordDesiredRole(
                            "general:player",
                            "Player",
                            [userId])
                    ],
                    [
                        new DiscordDesiredChannel(
                            "event",
                            "event",
                            DiscordManagedChannelKind.Category),
                        new DiscordDesiredChannel(
                            "event:announcements",
                            "announcements",
                            DiscordManagedChannelKind.Text,
                            "event")
                    ])
            ]
        };
        var client = new FakeDiscordBotClient();
        var sync = new DiscordWorkspaceSyncService(
            db,
            [source],
            client,
            TimeProvider.System,
            NullLogger<DiscordWorkspaceSyncService>.Instance);

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Equal(("100", "Player"), client.CreatedRoles[0]);
        Assert.Single(client.AddedRoles);
        Assert.Equal("777", client.AddedRoles[0].UserId);
        Assert.Equal(2, client.CreatedChannels.Count);
        Assert.Equal(
            DiscordManagedChannelKind.Category,
            client.CreatedChannels[0].Kind);
        Assert.Equal(
            client.CreatedChannels[0].ChannelId,
            client.CreatedChannels[1].ParentId);
        Assert.Single(await db.DiscordManagedRoles.ToListAsync());
        Assert.Single(await db.DiscordManagedRoleAssignments.ToListAsync());
        Assert.Equal(2, await db.DiscordManagedChannels.CountAsync());

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Single(client.AddedRoles);
        Assert.Equal(2, client.CreatedChannels.Count);

        source.Projections =
        [
            new DiscordGuildWorkspaceProjection(
                "dorks-and-dice",
                "100",
                "100",
                [],
                [])
        ];

        await sync.SynchronizeAsync();

        Assert.Single(client.RemovedRoles);
        Assert.Single(client.DeletedRoles);
        Assert.Equal(2, client.DeletedChannels.Count);
        Assert.Empty(await db.DiscordManagedRoles.ToListAsync());
        Assert.Empty(await db.DiscordManagedRoleAssignments.ToListAsync());
        Assert.Empty(await db.DiscordManagedChannels.ToListAsync());
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
                new DiscordGuildWorkspaceProjection(
                    "dorks-and-dice",
                    "100",
                    "100",
                    [
                        new DiscordDesiredRole(
                            "general:dm",
                            "DM",
                            [userId])
                    ],
                    [])
            ]
        };
        var client = new FakeDiscordBotClient();
        var sync = new DiscordWorkspaceSyncService(
            db,
            [source],
            client,
            TimeProvider.System,
            NullLogger<DiscordWorkspaceSyncService>.Instance);

        await sync.SynchronizeAsync();

        Assert.Single(client.CreatedRoles);
        Assert.Empty(client.AddedRoles);
        Assert.Empty(await db.DiscordManagedRoleAssignments.ToListAsync());
    }

    [Fact]
    public async Task OwnershipVerifierUsesLinkedDiscordIdentityAndGuildOwner()
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
            UserName = "owner@example.test",
            NormalizedUserName = "OWNER@EXAMPLE.TEST",
            Email = "owner@example.test",
            NormalizedEmail = "OWNER@EXAMPLE.TEST",
            DisplayName = "Owner",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>
        {
            LoginProvider = DiscordProvider.Id,
            ProviderKey = "123",
            ProviderDisplayName = "Discord",
            UserId = userId
        });
        await db.SaveChangesAsync();

        var client = new FakeDiscordBotClient();
        client.Guilds["100"] = new DiscordGuild("100", "Owned Server", "123");
        client.Guilds["200"] = new DiscordGuild("200", "Other Server", "456");

        var verifier = new DiscordGuildOwnershipVerifier(db, client);

        var owned = await verifier.VerifyAsync(userId, "100");
        Assert.True(owned.IsVerified);
        Assert.Equal("Owned Server", owned.GuildName);

        var notOwned = await verifier.VerifyAsync(userId, "200");
        Assert.Equal(DiscordGuildOwnershipStatus.NotOwner, notOwned.Status);

        var missing = await verifier.VerifyAsync(userId, "300");
        Assert.Equal(DiscordGuildOwnershipStatus.BotNotInstalled, missing.Status);
    }

    private sealed class MutableProjectionSource : IDiscordWorkspaceProjectionSource
    {
        public string SourceId => "test-source";

        public IReadOnlyCollection<DiscordGuildWorkspaceProjection> Projections { get; set; } = [];

        public Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Projections);
    }

    private sealed class FakeDiscordBotClient : IDiscordBotClient
    {
        private int _nextRoleId = 900;
        private int _nextChannelId = 1200;

        public Dictionary<string, DiscordGuild> Guilds { get; } = new(StringComparer.Ordinal);
        public List<(string GuildId, string Name)> CreatedRoles { get; } = [];
        public List<(string GuildId, string RoleId, string Name)> UpdatedRoles { get; } = [];
        public List<(string GuildId, string RoleId)> DeletedRoles { get; } = [];
        public List<(string GuildId, string UserId, string RoleId)> AddedRoles { get; } = [];
        public List<(string GuildId, string UserId, string RoleId)> RemovedRoles { get; } = [];
        public List<(string GuildId, string ChannelId, string Name, DiscordManagedChannelKind Kind, string? ParentId)> CreatedChannels { get; } = [];
        public List<string> DeletedChannels { get; } = [];

        private readonly Dictionary<string, List<DiscordGuildRole>> _roles =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<DiscordGuildChannel>> _channels =
            new(StringComparer.Ordinal);

        public Task<DiscordGuild?> GetGuildAsync(
            string guildId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Guilds.TryGetValue(guildId, out var guild)
                    ? guild
                    : null);

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

        public Task<IReadOnlyList<DiscordGuildChannel>> GetGuildChannelsAsync(
            string guildId,
            CancellationToken cancellationToken = default)
        {
            if (!_channels.TryGetValue(guildId, out var channels))
            {
                channels = [];
                _channels[guildId] = channels;
            }

            return Task.FromResult<IReadOnlyList<DiscordGuildChannel>>(channels.ToArray());
        }

        public Task<DiscordGuildChannel> CreateGuildChannelAsync(
            string guildId,
            string name,
            DiscordManagedChannelKind kind,
            string? parentId,
            CancellationToken cancellationToken = default)
        {
            var channel = new DiscordGuildChannel(
                (++_nextChannelId).ToString(),
                name,
                kind,
                parentId);
            if (!_channels.TryGetValue(guildId, out var channels))
            {
                channels = [];
                _channels[guildId] = channels;
            }

            channels.Add(channel);
            CreatedChannels.Add((guildId, channel.Id, name, kind, parentId));
            return Task.FromResult(channel);
        }

        public Task UpdateGuildChannelAsync(
            string channelId,
            string name,
            string? parentId,
            CancellationToken cancellationToken = default)
        {
            foreach (var channels in _channels.Values)
            {
                var index = channels.FindIndex(channel => channel.Id == channelId);
                if (index < 0)
                {
                    continue;
                }

                var existing = channels[index];
                channels[index] = existing with
                {
                    Name = name,
                    ParentId = parentId
                };
                break;
            }

            return Task.CompletedTask;
        }

        public Task DeleteGuildChannelAsync(
            string channelId,
            CancellationToken cancellationToken = default)
        {
            foreach (var channels in _channels.Values)
            {
                channels.RemoveAll(channel => channel.Id == channelId);
            }

            DeletedChannels.Add(channelId);
            return Task.CompletedTask;
        }
    }
}
