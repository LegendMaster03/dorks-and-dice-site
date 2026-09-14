using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceCampaignDomainTests
{
    [Fact]
    public async Task CampaignCreatorReceivesCampaignScopedDmRole()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var creator = Guid.NewGuid();

        var campaign = await harness.Campaigns.CreateAsync(creator, "Fool's Gold");

        Assert.True(await harness.Access.HasRoleAsync(creator, campaign.Id, CampaignRoles.Dm));
        Assert.False(await harness.Access.HasRoleAsync(creator, campaign.Id, CampaignRoles.Player));
    }

    [Fact]
    public async Task RolesAreIndependentAcrossCampaigns()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var aliceCampaign = await harness.Campaigns.CreateAsync(alice, "Alice Campaign");
        var bobCampaign = await harness.Campaigns.CreateAsync(bob, "Bob Campaign");
        await harness.Campaigns.AddMemberAsync(
            bob,
            bobCampaign.Id,
            alice,
            [CampaignRoles.Player]);

        Assert.True(await harness.Access.HasRoleAsync(alice, aliceCampaign.Id, CampaignRoles.Dm));
        Assert.True(await harness.Access.HasRoleAsync(alice, bobCampaign.Id, CampaignRoles.Player));
        Assert.False(await harness.Access.HasRoleAsync(alice, bobCampaign.Id, CampaignRoles.Dm));
    }

    [Fact]
    public async Task ActiveMembershipRequiresAtLeastOneRole()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");

        var exception = await Assert.ThrowsAsync<CampaignDomainException>(() =>
            harness.Campaigns.AddMemberAsync(dm, campaign.Id, Guid.NewGuid(), Array.Empty<string>()));

        Assert.Contains("at least one role", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoleDmCanNotLeaveUntilAnotherDmExists()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var originalDm = Guid.NewGuid();
        var replacementDm = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(originalDm, "Campaign");

        var exception = await Assert.ThrowsAsync<CampaignDomainException>(() =>
            harness.Campaigns.LeaveAsync(originalDm, campaign.Id));
        Assert.Contains("at least one active DM", exception.Message, StringComparison.OrdinalIgnoreCase);

        await harness.Campaigns.AddMemberAsync(
            originalDm,
            campaign.Id,
            replacementDm,
            [CampaignRoles.Dm]);
        await harness.Campaigns.LeaveAsync(originalDm, campaign.Id);

        Assert.False(await harness.Access.IsMemberAsync(originalDm, campaign.Id));
        Assert.True(await harness.Access.HasRoleAsync(replacementDm, campaign.Id, CampaignRoles.Dm));
    }

    [Fact]
    public async Task PlayerLeavingEndsCampaignConnectionButKeepsCharacterOwnership()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var character = await harness.Characters.CreateAsync(player, "Kell");
        await harness.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);

        await harness.Campaigns.LeaveAsync(player, campaign.Id);

        var savedCharacter = await harness.Characters.GetAsync(player, character.Id);
        Assert.NotNull(savedCharacter);
        Assert.Equal(player, savedCharacter.OwnerUserId);
        var association = Assert.Single(savedCharacter.CampaignAssociations);
        Assert.Equal(CampaignCharacterAssociationStatus.Ended, association.Status);
        Assert.False(await harness.Access.IsMemberAsync(player, campaign.Id));
    }

    [Fact]
    public async Task DmRemovingPlayerEndsCampaignConnectionButKeepsCharacterOwnership()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var character = await harness.Characters.CreateAsync(player, "Character");
        await harness.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);

        await harness.Campaigns.RemoveMemberAsync(dm, campaign.Id, player);

        var savedCharacter = await harness.Characters.GetAsync(player, character.Id);
        Assert.NotNull(savedCharacter);
        Assert.Equal(player, savedCharacter.OwnerUserId);
        var association = Assert.Single(savedCharacter.CampaignAssociations);
        Assert.Equal(CampaignCharacterAssociationStatus.Ended, association.Status);
        Assert.False(await harness.Access.IsMemberAsync(player, campaign.Id));
    }

    [Fact]
    public async Task CharacterHasOnlyOneActiveCampaignConnectionButRetainsHistory()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var firstDm = Guid.NewGuid();
        var secondDm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var firstCampaign = await harness.Campaigns.CreateAsync(firstDm, "First");
        var secondCampaign = await harness.Campaigns.CreateAsync(secondDm, "Second");
        await harness.Campaigns.AddMemberAsync(firstDm, firstCampaign.Id, player, [CampaignRoles.Player]);
        await harness.Campaigns.AddMemberAsync(secondDm, secondCampaign.Id, player, [CampaignRoles.Player]);
        var character = await harness.Characters.CreateAsync(player, "Traveler");

        await harness.Characters.ConnectToCampaignAsync(player, character.Id, firstCampaign.Id);
        await Assert.ThrowsAsync<CampaignDomainException>(() =>
            harness.Characters.ConnectToCampaignAsync(player, character.Id, secondCampaign.Id));

        await harness.Characters.DisconnectFromCampaignAsync(player, character.Id, firstCampaign.Id);
        await harness.Characters.ConnectToCampaignAsync(player, character.Id, secondCampaign.Id);

        var savedCharacter = await harness.Characters.GetAsync(player, character.Id);
        Assert.NotNull(savedCharacter);
        Assert.Equal(2, savedCharacter.CampaignAssociations.Count);
        Assert.Single(
            savedCharacter.CampaignAssociations,
            association => association.Status == CampaignCharacterAssociationStatus.Active
                && association.CampaignId == secondCampaign.Id);
    }

    [Fact]
    public async Task GuestParticipantCanExistWithoutAccountAndLaterBeLinked()
    {
        await using var harness = await CampaignHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);

        var participant = await harness.Participants.AddGuestAsync(dm, campaign.Id, "Table Player");
        Assert.Null(participant.UserId);

        await harness.Participants.LinkToUserAsync(dm, campaign.Id, participant.Id, player);

        var participants = await harness.Participants.GetForCampaignAsync(dm, campaign.Id);
        var linked = Assert.Single(participants);
        Assert.Equal(player, linked.UserId);
    }

    private sealed class CampaignHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CampaignHarness(SqliteConnection connection, DorksAndDiceDbContext db)
        {
            _connection = connection;
            Db = db;
            Access = new CampaignAccessService(db);
            Campaigns = new CampaignService(db, Access, TimeProvider.System);
            Participants = new CampaignParticipantService(db, Access, TimeProvider.System);
            Characters = new CharacterService(db, Access, TimeProvider.System);
        }

        public DorksAndDiceDbContext Db { get; }
        public CampaignAccessService Access { get; }
        public CampaignService Campaigns { get; }
        public CampaignParticipantService Participants { get; }
        public CharacterService Characters { get; }

        public static async Task<CampaignHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new DorksAndDiceDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new CampaignHarness(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
