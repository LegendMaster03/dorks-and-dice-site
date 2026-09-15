using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDicePermanentDeletionTests
{
    [Fact]
    public async Task CharacterOwnerCanPermanentlyDeleteCharacterAndAssociationHistory()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Campaign");
        await h.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var character = await h.Characters.CreateAsync(player, "Kell");
        await h.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);
        await h.Characters.ArchiveAsync(player, character.Id);

        Assert.True(await h.Db.CampaignCharacters.AnyAsync(item => item.CharacterId == character.Id));

        await h.Deletion.DeleteCharacterAsync(player, character.Id);

        Assert.False(await h.Db.Characters.AnyAsync(item => item.Id == character.Id));
        Assert.False(await h.Db.CampaignCharacters.AnyAsync(item => item.CharacterId == character.Id));
        Assert.True(await h.Db.Campaigns.AnyAsync(item => item.Id == campaign.Id));
        Assert.True(await h.Db.CampaignMemberships.AnyAsync(item => item.CampaignId == campaign.Id && item.UserId == player));
    }

    [Fact]
    public async Task AnotherUserCanNotPermanentlyDeleteCharacter()
    {
        await using var h = await Harness.CreateAsync();
        var owner = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var character = await h.Characters.CreateAsync(owner, "Kell");

        await Assert.ThrowsAsync<CampaignDomainException>(() =>
            h.Deletion.DeleteCharacterAsync(otherUser, character.Id));

        Assert.True(await h.Db.Characters.AnyAsync(item => item.Id == character.Id));
    }

    [Fact]
    public async Task DmCanPermanentlyDeleteActiveCampaignWithoutDeletingPlayerCharacters()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Campaign");
        await h.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var participant = await h.Participants.AddGuestAsync(dm, campaign.Id, "Invited Player");
        await h.Invitations.CreateAsync(dm, campaign.Id, [CampaignRoles.Player], participant.Id);
        var character = await h.Characters.CreateAsync(player, "Kell");
        await h.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);

        await h.Deletion.DeleteCampaignAsync(dm, campaign.Id);

        Assert.False(await h.Db.Campaigns.AnyAsync(item => item.Id == campaign.Id));
        Assert.False(await h.Db.CampaignMemberships.AnyAsync(item => item.CampaignId == campaign.Id));
        Assert.Empty(await h.Db.CampaignMembershipRoles.ToListAsync());
        Assert.False(await h.Db.CampaignParticipants.AnyAsync(item => item.CampaignId == campaign.Id));
        Assert.False(await h.Db.CampaignInvitations.AnyAsync(item => item.CampaignId == campaign.Id));
        Assert.False(await h.Db.CampaignCharacters.AnyAsync(item => item.CampaignId == campaign.Id));
        Assert.True(await h.Db.Characters.AnyAsync(item => item.Id == character.Id && item.OwnerUserId == player));
    }

    [Fact]
    public async Task DmCanPermanentlyDeleteArchivedCampaign()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Campaign");
        await h.Campaigns.ArchiveAsync(dm, campaign.Id);

        await h.Deletion.DeleteCampaignAsync(dm, campaign.Id);

        Assert.False(await h.Db.Campaigns.AnyAsync(item => item.Id == campaign.Id));
    }

    [Fact]
    public async Task PlayerCanNotPermanentlyDeleteCampaign()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Campaign");
        await h.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);

        await Assert.ThrowsAsync<CampaignDomainException>(() =>
            h.Deletion.DeleteCampaignAsync(player, campaign.Id));

        Assert.True(await h.Db.Campaigns.AnyAsync(item => item.Id == campaign.Id));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Harness(SqliteConnection connection, DorksAndDiceDbContext db)
        {
            _connection = connection;
            Db = db;
            Access = new CampaignAccessService(db);
            Campaigns = new CampaignService(db, Access, TimeProvider.System);
            Participants = new CampaignParticipantService(db, Access, TimeProvider.System);
            Invitations = new CampaignInvitationService(db, Access, TimeProvider.System);
            Characters = new CharacterService(db, Access, TimeProvider.System);
            Deletion = new DorksAndDiceDeletionService(db);
        }

        public DorksAndDiceDbContext Db { get; }
        public CampaignAccessService Access { get; }
        public CampaignService Campaigns { get; }
        public CampaignParticipantService Participants { get; }
        public CampaignInvitationService Invitations { get; }
        public CharacterService Characters { get; }
        public DorksAndDiceDeletionService Deletion { get; }

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new DorksAndDiceDbContext(
                new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await db.Database.EnsureCreatedAsync();
            return new Harness(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
