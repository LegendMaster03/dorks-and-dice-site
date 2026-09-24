using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Site;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceDiscordRoleProjectionTests
{
    [Fact]
    public async Task GeneralAndCampaignGuildsProjectIndependentPlayerAndDmRoles()
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

        db.CampaignDiscordGuildBindings.Add(new CampaignDiscordGuildBinding
        {
            CampaignId = campaign.Id,
            GuildId = "200",
            ConfiguredByUserId = dmOnly,
            ConfiguredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordRoleProjectionSource(
            db,
            new TestModeConnections("100"));

        var projections = await source.BuildAsync();

        Assert.Equal(2, projections.Count);

        var general = Assert.Single(projections, projection => projection.GuildId == "100");
        Assert.Equal("100", general.ActivationResourceId);
        var generalPlayer = Assert.Single(general.Roles, role => role.Key == "general:player");
        Assert.Equal(
            new[] { both, playerOnly }.Order(),
            generalPlayer.UserIds.Order());
        var generalDm = Assert.Single(general.Roles, role => role.Key == "general:dm");
        Assert.Equal(
            new[] { both, dmOnly }.Order(),
            generalDm.UserIds.Order());
        var campaignRole = Assert.Single(
            general.Roles,
            role => role.Key == $"general:campaign:{campaign.Id:N}");
        Assert.Equal("Campaign: Humblewood", campaignRole.DisplayName);
        Assert.Equal(
            new[] { both, dmOnly, playerOnly }.Order(),
            campaignRole.UserIds.Order());

        var dedicated = Assert.Single(projections, projection => projection.GuildId == "200");
        Assert.Equal(2, dedicated.Roles.Count);
        var dedicatedPlayer = Assert.Single(
            dedicated.Roles,
            role => role.DisplayName == "Player");
        Assert.Equal(
            new[] { both, playerOnly }.Order(),
            dedicatedPlayer.UserIds.Order());
        var dedicatedDm = Assert.Single(
            dedicated.Roles,
            role => role.DisplayName == "DM");
        Assert.Equal(
            new[] { both, dmOnly }.Order(),
            dedicatedDm.UserIds.Order());

        Assert.DoesNotContain(
            projections.SelectMany(projection => projection.Roles),
            role => role.DisplayName.Contains('+', StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArchivedCampaignKeepsDedicatedGuildProjectionButNoActiveRoles()
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
        var dm = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dm, "Archived");
        db.CampaignDiscordGuildBindings.Add(new CampaignDiscordGuildBinding
        {
            CampaignId = campaign.Id,
            GuildId = "200",
            ConfiguredByUserId = dm,
            ConfiguredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        await campaigns.ArchiveAsync(dm, campaign.Id);

        var source = new DorksAndDiceDiscordRoleProjectionSource(
            db,
            new TestModeConnections("100"));

        var projections = await source.BuildAsync();

        var general = Assert.Single(projections, projection => projection.GuildId == "100");
        Assert.DoesNotContain(
            general.Roles,
            role => role.Key == $"general:campaign:{campaign.Id:N}");

        var dedicated = Assert.Single(projections, projection => projection.GuildId == "200");
        Assert.Empty(dedicated.Roles);
    }

    [Fact]
    public async Task DedicatedGuildCanNotReuseGeneralGuildOrAnotherCampaignGuild()
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
        var modeConnections = new TestModeConnections("100");
        var service = new CampaignDiscordGuildService(
            db,
            access,
            modeConnections,
            TimeProvider.System);

        var firstDm = Guid.NewGuid();
        var secondDm = Guid.NewGuid();
        var first = await campaigns.CreateAsync(firstDm, "First");
        var second = await campaigns.CreateAsync(secondDm, "Second");

        await Assert.ThrowsAsync<CampaignDomainException>(() =>
            service.SetAsync(firstDm, first.Id, "100"));

        await service.SetAsync(firstDm, first.Id, "200");

        await Assert.ThrowsAsync<CampaignDomainException>(() =>
            service.SetAsync(secondDm, second.Id, "200"));
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
