using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceInvitationExpirationTests
{
    [Fact]
    public async Task ExpiredParticipantInviteCanBeReplaced()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new DorksAndDiceDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, clock);
        var participants = new CampaignParticipantService(db, access, clock);
        var invitations = new CampaignInvitationService(db, access, clock);
        var dm = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dm, "Campaign");
        var participant = await participants.AddGuestAsync(dm, campaign.Id, "Player");

        var first = await invitations.CreateAsync(
            dm,
            campaign.Id,
            [CampaignRoles.Player],
            participant.Id);

        clock.Advance(TimeSpan.FromDays(8));

        var replacement = await invitations.CreateAsync(
            dm,
            campaign.Id,
            [CampaignRoles.Player],
            participant.Id);

        var stored = (await db.CampaignInvitations
            .AsNoTracking()
            .ToListAsync())
            .OrderBy(invitation => invitation.CreatedAt)
            .ToList();

        Assert.Equal(2, stored.Count);
        Assert.Equal(CampaignInvitationStatus.Expired, stored[0].Status);
        Assert.Equal(CampaignInvitationStatus.Pending, stored[1].Status);
        Assert.Null(await invitations.GetPreviewAsync(first.Token));
        Assert.NotNull(await invitations.GetPreviewAsync(replacement.Token));
    }

    [Fact]
    public async Task ListingPendingInvitesPersistsExpirationState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new DorksAndDiceDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, clock);
        var invitations = new CampaignInvitationService(db, access, clock);
        var dm = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dm, "Campaign");
        var grant = await invitations.CreateAsync(dm, campaign.Id, [CampaignRoles.Player]);

        clock.Advance(TimeSpan.FromDays(8));

        Assert.Empty(await invitations.GetPendingAsync(dm, campaign.Id));
        var stored = await db.CampaignInvitations.AsNoTracking().SingleAsync();
        Assert.Equal(CampaignInvitationStatus.Expired, stored.Status);
        Assert.Null(await invitations.GetPreviewAsync(grant.Token));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
