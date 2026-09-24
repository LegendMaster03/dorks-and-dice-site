using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceDiscordLinkedAccountProjectionTests
{
    [Fact]
    public async Task ProjectionIncludesOnlyDiscordLinkedUsersActivatedForDorksAndDice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var linkedAndActive = CreateUser("linked-active");
        var linkedOnly = CreateUser("linked-only");
        var activationOnly = CreateUser("activation-only");
        var wrongResource = CreateUser("wrong-resource");

        db.Users.AddRange(
            linkedAndActive,
            linkedOnly,
            activationOnly,
            wrongResource);

        db.UserLogins.AddRange(
            DiscordLogin(linkedAndActive.Id, "1001"),
            DiscordLogin(linkedOnly.Id, "1002"),
            DiscordLogin(wrongResource.Id, "1004"));

        db.AccountLinkModeActivations.AddRange(
            Activation(linkedAndActive.Id, "main-guild"),
            Activation(activationOnly.Id, "main-guild"),
            Activation(wrongResource.Id, "other-guild"));

        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordLinkedAccountProjectionSource(
            db,
            new TestModeConnections("main-guild"));

        var projection = Assert.Single(await source.BuildAsync());
        Assert.Equal("main-guild", projection.GuildId);

        var role = Assert.Single(projection.Roles);
        Assert.Equal("linked-account", role.Key);
        Assert.Equal("Linked Account", role.DisplayName);
        Assert.Equal([linkedAndActive.Id], role.UserIds);
    }

    [Fact]
    public async Task ProjectionIsEmptyWhenDorksAndDiceHasNoDiscordConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var source = new DorksAndDiceDiscordLinkedAccountProjectionSource(
            db,
            new EmptyModeConnections());

        Assert.Empty(await source.BuildAsync());
    }

    private static ApplicationUser CreateUser(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserName = $"{name}@example.test",
            NormalizedUserName = $"{name}@example.test".ToUpperInvariant(),
            Email = $"{name}@example.test",
            NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
            DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static IdentityUserLogin<Guid> DiscordLogin(Guid userId, string providerKey) =>
        new()
        {
            UserId = userId,
            LoginProvider = DiscordProvider.Id,
            ProviderKey = providerKey,
            ProviderDisplayName = "Discord"
        };

    private static AccountLinkModeActivation Activation(Guid userId, string resourceId) =>
        new()
        {
            UserId = userId,
            ModeId = SiteModeValues.DorksAndDiceModeValue,
            ProviderId = DiscordProvider.Id,
            ResourceId = resourceId,
            ActivatedAt = DateTimeOffset.UtcNow
        };

    private sealed class TestModeConnections(string resourceId)
        : IModeExternalConnectionRegistry
    {
        private readonly ModeExternalConnection _connection = new(
            SiteModeValues.DorksAndDiceModeValue,
            DiscordProvider.Id,
            resourceId);

        public IReadOnlyList<ModeExternalConnection> All => [_connection];

        public IReadOnlyList<ModeExternalConnection> GetForMode(string modeId) =>
            string.Equals(
                modeId,
                SiteModeValues.DorksAndDiceModeValue,
                StringComparison.Ordinal)
                ? [_connection]
                : [];

        public bool TryGet(
            string modeId,
            string providerId,
            out ModeExternalConnection? connection)
        {
            if (string.Equals(
                    modeId,
                    SiteModeValues.DorksAndDiceModeValue,
                    StringComparison.Ordinal)
                && string.Equals(
                    providerId,
                    DiscordProvider.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                connection = _connection;
                return true;
            }

            connection = null;
            return false;
        }
    }

    private sealed class EmptyModeConnections : IModeExternalConnectionRegistry
    {
        public IReadOnlyList<ModeExternalConnection> All => [];

        public IReadOnlyList<ModeExternalConnection> GetForMode(string modeId) => [];

        public bool TryGet(
            string modeId,
            string providerId,
            out ModeExternalConnection? connection)
        {
            connection = null;
            return false;
        }
    }
}
