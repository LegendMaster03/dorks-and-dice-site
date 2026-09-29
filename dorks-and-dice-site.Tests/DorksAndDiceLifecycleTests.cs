using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceLifecycleTests
{
    [Fact]
    public async Task CampaignArchivePreservesMembershipsButEndsLiveCharacterLinksAndInvites()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Before");
        await h.Campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);
        var character = await h.Characters.CreateAsync(player, "Kell");
        await h.Characters.ConnectToCampaignAsync(player, character.Id, campaign.Id);
        var invite = await h.Invitations.CreateAsync(dm, campaign.Id, [CampaignRoles.Player]);

        await h.Campaigns.RenameAsync(dm, campaign.Id, "After");
        await h.Campaigns.ArchiveAsync(dm, campaign.Id);

        Assert.False(await h.Access.IsMemberAsync(player, campaign.Id));
        Assert.Null(await h.Invitations.GetPreviewAsync(invite.Token));
        var archived = Assert.Single(await h.Campaigns.GetArchivedForUserAsync(dm));
        Assert.Equal("After", archived.Name);
        var savedCharacter = await h.Characters.GetAsync(player, character.Id);
        Assert.NotNull(savedCharacter);
        Assert.Single(savedCharacter.CampaignAssociations, item => item.Status == CampaignCharacterAssociationStatus.Ended && item.EndReason == "Campaign archived");

        await h.Campaigns.RestoreAsync(dm, campaign.Id);
        Assert.True(await h.Access.IsMemberAsync(player, campaign.Id));
        var restoredCharacter = await h.Characters.GetAsync(player, character.Id);
        Assert.NotNull(restoredCharacter);
        Assert.DoesNotContain(restoredCharacter.CampaignAssociations, item => item.Status == CampaignCharacterAssociationStatus.Active);
    }

    [Fact]
    public async Task ArchivedCampaignIsReadOnlyUntilRestored()
    {
        await using var h = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var campaign = await h.Campaigns.CreateAsync(dm, "Original");

        await h.Campaigns.ArchiveAsync(dm, campaign.Id);

        await Assert.ThrowsAsync<CampaignDomainException>(() => h.Campaigns.RenameAsync(dm, campaign.Id, "Archived Rename"));
        var archived = Assert.Single(await h.Campaigns.GetArchivedForUserAsync(dm));
        Assert.Equal("Original", archived.Name);

        await h.Campaigns.RestoreAsync(dm, campaign.Id);
        await h.Campaigns.RenameAsync(dm, campaign.Id, "Restored Rename");
        var restored = await h.Campaigns.GetAsync(dm, campaign.Id);
        Assert.NotNull(restored);
        Assert.Equal("Restored Rename", restored.Name);
    }

    [Fact]
    public async Task CharacterArchiveEndsAllCampaignLinksAndRestoreDoesNotReconnect()
    {
        await using var h = await Harness.CreateAsync();
        var dm1 = Guid.NewGuid();
        var dm2 = Guid.NewGuid();
        var player = Guid.NewGuid();
        var first = await h.Campaigns.CreateAsync(dm1, "First");
        var second = await h.Campaigns.CreateAsync(dm2, "Second");
        await h.Campaigns.AddMemberAsync(dm1, first.Id, player, [CampaignRoles.Player]);
        await h.Campaigns.AddMemberAsync(dm2, second.Id, player, [CampaignRoles.Player]);
        var character = await h.Characters.CreateAsync(player, "Old Name");
        await h.Characters.ConnectToCampaignAsync(player, character.Id, first.Id);
        await h.Characters.ConnectToCampaignAsync(player, character.Id, second.Id);

        await h.Characters.RenameAsync(player, character.Id, "New Name");
        await h.Characters.ArchiveAsync(player, character.Id);

        var archived = await h.Characters.GetAsync(player, character.Id);
        Assert.NotNull(archived);
        Assert.Equal("New Name", archived.Name);
        Assert.Equal(CharacterStatus.Archived, archived.Status);
        Assert.Equal(2, archived.CampaignAssociations.Count(item => item.Status == CampaignCharacterAssociationStatus.Ended));

        await h.Characters.RestoreAsync(player, character.Id);
        var restored = await h.Characters.GetAsync(player, character.Id);
        Assert.NotNull(restored);
        Assert.Equal(CharacterStatus.Active, restored.Status);
        Assert.DoesNotContain(restored.CampaignAssociations, item => item.Status == CampaignCharacterAssociationStatus.Active);
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
            Invitations = new CampaignInvitationService(db, Access, TimeProvider.System);
            Characters = new CharacterService(db, Access, TimeProvider.System);
        }
        public DorksAndDiceDbContext Db { get; }
        public CampaignAccessService Access { get; }
        public CampaignService Campaigns { get; }
        public CampaignInvitationService Invitations { get; }
        public CharacterService Characters { get; }
        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new DorksAndDiceDbContext(new DbContextOptionsBuilder<DorksAndDiceDbContext>().UseSqlite(connection).Options);
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
