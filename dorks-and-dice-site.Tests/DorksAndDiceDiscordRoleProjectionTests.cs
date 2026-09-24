using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Site;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceDiscordProjectionTests
{
    [Fact]
    public async Task MainServerIsSpecialAndSingleCampaignServerDoesNotNeedCampaignRole()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new DorksAndDiceDbContext(
            new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, TimeProvider.System);

        var dmOnly = Guid.NewGuid();
        var playerOnly = Guid.NewGuid();
        var both = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dmOnly, "Humblewood");
        await campaigns.AddMemberAsync(
            dmOnly,
            campaign.Id,
            playerOnly,
            [CampaignRoles.Player]);
        await campaigns.AddMemberAsync(
            dmOnly,
            campaign.Id,
            both,
            [CampaignRoles.Player, CampaignRoles.Dm]);

        var bindingId = Guid.NewGuid();
        db.DiscordServerBindings.Add(new DorksAndDiceDiscordServerBinding
        {
            Id = bindingId,
            GuildId = "200",
            GuildName = "Humblewood Server",
            OwnerUserId = dmOnly,
            CampaignScope = DiscordServerCampaignScope.SingleCampaign,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Campaigns =
            [
                new DorksAndDiceDiscordServerCampaign
                {
                    BindingId = bindingId,
                    CampaignId = campaign.Id
                }
            ]
        });
        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordCampaignProjectionSource(
            db,
            new TestModeConnections("100"));

        var projections = await source.BuildAsync();

        Assert.Equal(2, projections.Count);

        var main = Assert.Single(projections, projection => projection.GuildId == "100");
        var mainPlayer = Assert.Single(main.Roles, role => role.Key == "main:player");
        Assert.Equal(
            new[] { both, playerOnly }.Order(),
            mainPlayer.UserIds.Order());
        var mainDm = Assert.Single(main.Roles, role => role.Key == "main:dm");
        Assert.Equal(
            new[] { both, dmOnly }.Order(),
            mainDm.UserIds.Order());
        var mainCampaign = Assert.Single(
            main.Roles,
            role => role.Key == $"main:campaign:{campaign.Id:N}");
        Assert.Equal("Campaign: Humblewood", mainCampaign.DisplayName);

        var dedicated = Assert.Single(projections, projection => projection.GuildId == "200");
        Assert.Equal(2, dedicated.Roles.Count);
        Assert.Contains(dedicated.Roles, role => role.DisplayName == "Player");
        Assert.Contains(dedicated.Roles, role => role.DisplayName == "DM");
        Assert.DoesNotContain(
            dedicated.Roles,
            role => role.DisplayName.StartsWith("Campaign:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            projections.SelectMany(projection => projection.Roles),
            role => role.DisplayName.Contains('+', StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllDmCampaignsScopeFollowsCurrentDmMemberships()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new DorksAndDiceDbContext(
            new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, TimeProvider.System);
        var owner = Guid.NewGuid();
        var otherDm = Guid.NewGuid();

        var first = await campaigns.CreateAsync(owner, "First");
        var second = await campaigns.CreateAsync(owner, "Second");
        var notOwned = await campaigns.CreateAsync(otherDm, "Other");
        await campaigns.AddMemberAsync(
            otherDm,
            notOwned.Id,
            owner,
            [CampaignRoles.Player]);

        db.DiscordServerBindings.Add(new DorksAndDiceDiscordServerBinding
        {
            Id = Guid.NewGuid(),
            GuildId = "200",
            GuildName = "All Games",
            OwnerUserId = owner,
            CampaignScope = DiscordServerCampaignScope.AllDmCampaigns,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordCampaignProjectionSource(
            db,
            new TestModeConnections("100"));

        var projections = await source.BuildAsync();
        var server = Assert.Single(projections, projection => projection.GuildId == "200");

        Assert.Contains(
            server.Roles,
            role => role.Key.EndsWith($"campaign:{first.Id:N}", StringComparison.Ordinal));
        Assert.Contains(
            server.Roles,
            role => role.Key.EndsWith($"campaign:{second.Id:N}", StringComparison.Ordinal));
        Assert.DoesNotContain(
            server.Roles,
            role => role.Key.EndsWith($"campaign:{notOwned.Id:N}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SelectedCampaignsThatAreNoLongerDmOwnedDropOutOfProjection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new DorksAndDiceDbContext(
            new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, TimeProvider.System);
        var owner = Guid.NewGuid();
        var successor = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(owner, "Transferred");
        await campaigns.AddMemberAsync(
            owner,
            campaign.Id,
            successor,
            [CampaignRoles.Dm]);
        await campaigns.SetMemberRolesAsync(
            owner,
            campaign.Id,
            owner,
            [CampaignRoles.Player]);

        var bindingId = Guid.NewGuid();
        db.DiscordServerBindings.Add(new DorksAndDiceDiscordServerBinding
        {
            Id = bindingId,
            GuildId = "200",
            GuildName = "Transferred Server",
            OwnerUserId = owner,
            CampaignScope = DiscordServerCampaignScope.SelectedCampaigns,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Campaigns =
            [
                new DorksAndDiceDiscordServerCampaign
                {
                    BindingId = bindingId,
                    CampaignId = campaign.Id
                }
            ]
        });
        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordCampaignProjectionSource(
            db,
            new TestModeConnections("100"));

        var projection = Assert.Single(
            await source.BuildAsync(),
            item => item.GuildId == "200");

        Assert.Equal(2, projection.Roles.Count);
        Assert.All(
            projection.Roles,
            role => Assert.Empty(role.UserIds));
        Assert.DoesNotContain(
            projection.Roles,
            role => role.DisplayName.StartsWith("Campaign:", StringComparison.Ordinal));
    }

    private sealed class TestModeConnections(string generalGuildId)
        : IModeExternalConnectionRegistry
    {
        private readonly ModeExternalConnection _connection = new(
            SiteModeValues.DorksAndDiceModeValue,
            DiscordProvider.Id,
            generalGuildId);

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
}
