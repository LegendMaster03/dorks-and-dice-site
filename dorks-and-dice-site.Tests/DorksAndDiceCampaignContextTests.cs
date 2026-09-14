using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceCampaignContextTests
{
    [Fact]
    public async Task AccessibleCampaignsExposeStableIdentityAndScopedRoles()
    {
        await using var harness = await ContextHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var user = Guid.NewGuid();
        var first = await harness.Campaigns.CreateAsync(dm, "First");
        var second = await harness.Campaigns.CreateAsync(user, "Second");
        await harness.Campaigns.AddMemberAsync(dm, first.Id, user, [CampaignRoles.Player]);

        var contexts = await harness.Context.GetAccessibleCampaignsAsync(user);

        Assert.Equal(2, contexts.Count);
        var firstContext = Assert.Single(contexts, item => item.CampaignId == first.Id);
        Assert.Equal([CampaignRoles.Player], firstContext.Roles);
        var secondContext = Assert.Single(contexts, item => item.CampaignId == second.Id);
        Assert.Equal([CampaignRoles.Dm], secondContext.Roles);
    }

    [Fact]
    public async Task CampaignContextIncludesOnlyActiveParticipantAndCharacterReferences()
    {
        await using var harness = await ContextHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");
        await harness.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var participant = await harness.Participants.AddGuestAsync(dm, campaign.Id, "Player Name");
        await harness.Participants.LinkToUserAsync(dm, campaign.Id, participant.Id, player);
        var character = await harness.Characters.CreateAsync(player, "Kell");
        await harness.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);

        var activeContext = await harness.Context.GetCampaignContextAsync(dm, campaign.Id);

        Assert.NotNull(activeContext);
        var participantContext = Assert.Single(activeContext.Participants);
        Assert.Equal(player, participantContext.UserId);
        var characterContext = Assert.Single(activeContext.Characters);
        Assert.Equal(character.Id, characterContext.CharacterId);
        Assert.Equal(player, characterContext.OwnerUserId);
        Assert.Equal("Kell", characterContext.Name);

        await harness.Campaigns.RemoveMemberAsync(dm, campaign.Id, player);
        var afterRemoval = await harness.Context.GetCampaignContextAsync(dm, campaign.Id);

        Assert.NotNull(afterRemoval);
        Assert.Empty(afterRemoval.Participants);
        Assert.Empty(afterRemoval.Characters);
        Assert.NotNull(await harness.Characters.GetAsync(player, character.Id));
    }

    [Fact]
    public async Task NonMemberCanNotReadCampaignContext()
    {
        await using var harness = await ContextHarness.CreateAsync();
        var dm = Guid.NewGuid();
        var campaign = await harness.Campaigns.CreateAsync(dm, "Campaign");

        var context = await harness.Context.GetCampaignContextAsync(Guid.NewGuid(), campaign.Id);

        Assert.Null(context);
    }

    private sealed class ContextHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ContextHarness(SqliteConnection connection, DorksAndDiceDbContext db)
        {
            _connection = connection;
            Db = db;
            Access = new CampaignAccessService(db);
            Campaigns = new CampaignService(db, Access, TimeProvider.System);
            Participants = new CampaignParticipantService(db, Access, TimeProvider.System);
            Characters = new CharacterService(db, Access, TimeProvider.System);
            Context = new CampaignContextService(db, Access);
        }

        public DorksAndDiceDbContext Db { get; }
        public CampaignAccessService Access { get; }
        public CampaignService Campaigns { get; }
        public CampaignParticipantService Participants { get; }
        public CharacterService Characters { get; }
        public CampaignContextService Context { get; }

        public static async Task<ContextHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new DorksAndDiceDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new ContextHarness(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
