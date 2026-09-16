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

        var lifecycleEvent = Assert.Single(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
        Assert.Equal(ToolLifecycleTargets.CharacterSheet, lifecycleEvent.TargetToolSlug);
        Assert.Equal(ToolLifecycleEventTypes.CharacterDeleted, lifecycleEvent.EventType);
        Assert.Equal(character.Id, lifecycleEvent.SubjectId);
        Assert.Equal(0, lifecycleEvent.AttemptCount);
        Assert.Null(lifecycleEvent.DeliveredAt);
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
        Assert.Empty(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
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

        var lifecycleEvent = Assert.Single(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
        Assert.Equal(ToolLifecycleTargets.CharacterSheet, lifecycleEvent.TargetToolSlug);
        Assert.Equal(ToolLifecycleEventTypes.CampaignDeleted, lifecycleEvent.EventType);
        Assert.Equal(campaign.Id, lifecycleEvent.SubjectId);
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
        var lifecycleEvent = Assert.Single(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
        Assert.Equal(ToolLifecycleEventTypes.CampaignDeleted, lifecycleEvent.EventType);
        Assert.Equal(campaign.Id, lifecycleEvent.SubjectId);
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
        Assert.Empty(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
    }

    [Fact]
    public async Task OutboxInsertFailureRollsBackCanonicalCharacterDeletion()
    {
        await using var h = await Harness.CreateAsync();
        var owner = Guid.NewGuid();
        var character = await h.Characters.CreateAsync(owner, "Rollback Hero");

        await h.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_lifecycle_outbox
            BEFORE INSERT ON dd_tool_lifecycle_outbox
            BEGIN
                SELECT RAISE(ABORT, 'forced lifecycle outbox failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            h.Deletion.DeleteCharacterAsync(owner, character.Id));

        h.Db.ChangeTracker.Clear();
        Assert.True(await h.Db.Characters.AnyAsync(item => item.Id == character.Id));
        Assert.Empty(await h.Db.ToolLifecycleOutboxEvents.ToListAsync());
    }

    [Fact]
    public async Task PendingLifecycleEventPersistsAcrossNewDbContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dd-lifecycle-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        var owner = Guid.NewGuid();
        Guid characterId;

        try
        {
            var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using (var first = new DorksAndDiceDbContext(options))
            {
                await first.Database.EnsureCreatedAsync();
                var access = new CampaignAccessService(first);
                var characters = new CharacterService(first, access, TimeProvider.System);
                var deletion = new DorksAndDiceDeletionService(first, TimeProvider.System);
                var character = await characters.CreateAsync(owner, "Durable Hero");
                characterId = character.Id;
                await deletion.DeleteCharacterAsync(owner, characterId);
            }

            await using (var second = new DorksAndDiceDbContext(options))
            {
                Assert.False(await second.Characters.AnyAsync(item => item.Id == characterId));
                var lifecycleEvent = Assert.Single(await second.ToolLifecycleOutboxEvents.ToListAsync());
                Assert.Equal(ToolLifecycleEventTypes.CharacterDeleted, lifecycleEvent.EventType);
                Assert.Equal(characterId, lifecycleEvent.SubjectId);
                Assert.Null(lifecycleEvent.DeliveredAt);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
        }
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
            Deletion = new DorksAndDiceDeletionService(db, TimeProvider.System);
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
