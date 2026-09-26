using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace dorks_and_dice_site.Tests;

public sealed class DiscordManagedRoleMembershipReconcilerTests
{
    [Fact]
    public async Task RemovesSeededManagedRoleFromDiscordMemberWithoutSiteLink()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        db.DiscordManagedRoles.Add(new DiscordManagedRole
        {
            SourceId = "test-source",
            GuildId = "100",
            RoleKey = "general:player",
            DiscordRoleId = "900",
            DisplayName = "Player",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var source = new ProjectionSource(
            [
                new DiscordGuildWorkspaceProjection(
                    "dorks-and-dice",
                    "100",
                    "100",
                    [new DiscordDesiredRole("general:player", "Player", [])],
                    [])
            ]);
        var discord = new FakeDiscordBotClient
        {
            Members = [new DiscordGuildMember("777", ["900"])]
        };
        var reconciler = new DiscordManagedRoleMembershipReconciler(
            db,
            [source],
            discord,
            NullLogger<DiscordManagedRoleMembershipReconciler>.Instance);

        await reconciler.ReconcileAsync();

        Assert.Single(discord.RemovedRoles);
        Assert.Equal(("100", "777", "900"), discord.RemovedRoles[0]);
    }

    private sealed class ProjectionSource(IReadOnlyCollection<DiscordGuildWorkspaceProjection> projections)
        : IDiscordWorkspaceProjectionSource
    {
        public string SourceId => "test-source";

        public Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(projections);
    }

    private sealed class FakeDiscordBotClient : IDiscordBotClient
    {
        public IReadOnlyList<DiscordGuildMember> Members { get; init; } = [];
        public List<(string GuildId, string UserId, string RoleId)> RemovedRoles { get; } = [];

        public Task<IReadOnlyList<DiscordGuildMember>> GetGuildMembersAsync(
            string guildId,
            CancellationToken cancellationToken = default) => Task.FromResult(Members);

        public Task RemoveMemberRoleAsync(
            string guildId,
            string userId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            RemovedRoles.Add((guildId, userId, roleId));
            return Task.CompletedTask;
        }

        public Task<DiscordGuild?> GetGuildAsync(string guildId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<DiscordGuildRole>> GetGuildRolesAsync(string guildId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscordGuildRole> CreateGuildRoleAsync(string guildId, string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateGuildRoleAsync(string guildId, string roleId, string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteGuildRoleAsync(string guildId, string roleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AddMemberRoleAsync(string guildId, string userId, string roleId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<DiscordGuildChannel>> GetGuildChannelsAsync(string guildId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DiscordGuildChannel> CreateGuildChannelAsync(string guildId, string name, DiscordManagedChannelKind kind, string? parentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateGuildChannelAsync(string channelId, string name, string? parentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteGuildChannelAsync(string channelId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}