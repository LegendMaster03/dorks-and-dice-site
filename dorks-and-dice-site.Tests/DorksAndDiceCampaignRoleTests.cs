using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceCampaignRoleTests
{
    [Fact]
    public async Task DmCanPromoteAnotherMemberThenDemoteSelf()
    {
        await using var harness = await RoleHarness.CreateAsync();
        var firstDm = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(firstDm, "Campaign");
        await harness.Campaigns.AddMemberAsync(firstDm, campaign.Id, secondUser, [CampaignRoles.Player]);

        await harness.Campaigns.SetMemberRolesAsync(
            firstDm,
            campaign.Id,
            secondUser,
            [CampaignRoles.Player, CampaignRoles.Dm]);
        await harness.Campaigns.SetMemberRolesAsync(
            firstDm,
            campaign.Id,
            firstDm,
            [CampaignRoles.Player]);

        Assert.True(await harness.Access.HasRoleAsync(secondUser, campaign.Id, CampaignRoles.Dm));
        Assert.True(await harness.Access.HasRoleAsync(secondUser, campaign.Id, CampaignRoles.Player));
        Assert.False(await harness.Access.HasRoleAsync(firstDm, campaign.Id, CampaignRoles.Dm));
        Assert.True(await harness.Access.HasRoleAsync(firstDm, campaign.Id, CampaignRoles.Player));
    }

    [Fact]
    public async Task PlayerCanNotChangeCampaignRoles()
    {
        await using var harness = await RoleHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);

        var exception = await Assert.ThrowsAsync<CampaignDomainException>(() =>
            harness.Campaigns.SetMemberRolesAsync(
                player,
                campaign.Id,
                player,
                [CampaignRoles.Player, CampaignRoles.Dm]));

        Assert.Contains("role 'dm' is required", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await harness.Access.HasRoleAsync(player, campaign.Id, CampaignRoles.Dm));
    }

    [Fact]
    public async Task SoleDmCanNotRemoveOwnDmRole()
    {
        await using var harness = await RoleHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");

        var exception = await Assert.ThrowsAsync<CampaignDomainException>(() =>
            harness.Campaigns.SetMemberRolesAsync(dm, campaign.Id, dm, [CampaignRoles.Player]));

        Assert.Contains("at least one active DM", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(await harness.Access.HasRoleAsync(dm, campaign.Id, CampaignRoles.Dm));
    }

    [Fact]
    public async Task RemovingPlayerRoleEndsActiveCharacterConnectionWithoutDeletingCharacter()
    {
        await using var harness = await RoleHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var character = await harness.Characters.CreateAsync(player, "Kell");
        await harness.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);

        await harness.Campaigns.SetMemberRolesAsync(dm, campaign.Id, player, [CampaignRoles.Dm]);

        var savedCharacter = await harness.Characters.GetAsync(player, character.Id);
        Assert.NotNull(savedCharacter);
        Assert.Equal(player, savedCharacter.OwnerUserId);
        var association = Assert.Single(savedCharacter.CampaignAssociations);
        Assert.Equal(CampaignCharacterAssociationStatus.Ended, association.Status);
        Assert.Equal(dm, association.EndedByUserId);
        Assert.Equal("Player role removed", association.EndReason);
    }

    private sealed class RoleHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RoleHarness(SqliteConnection connection, DorksAndDiceDbContext db)
        {
            _connection = connection;
            Db = db;
            Access = new CampaignAccessService(db);
            Campaigns = new CampaignService(db, Access, TimeProvider.System);
            Characters = new CharacterService(db, Access, TimeProvider.System);
        }

        public DorksAndDiceDbContext Db { get; }
        public CampaignAccessService Access { get; }
        public CampaignService Campaigns { get; }
        public CharacterService Characters { get; }

        public static async Task<RoleHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new DorksAndDiceDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new RoleHarness(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
