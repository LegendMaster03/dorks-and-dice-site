using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceInvitationExpirationTests
{
    [Fact]
    public async Task ReusableInviteAcceptsMultipleAccountsUntilExpiration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>().UseSqlite(connection).Options;
        await using var db = new DorksAndDiceDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, clock);
        var invitations = new CampaignInvitationService(db, access, clock);
        var dm = Guid.NewGuid();
        var firstPlayer = Guid.NewGuid();
        var secondPlayer = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dm, "Campaign");
        var grant = await invitations.CreateAsync(dm, campaign.Id, [CampaignRoles.Player], isReusable: true);

        await invitations.AcceptAsync(firstPlayer, grant.Token);
        Assert.NotNull(await invitations.GetPreviewAsync(grant.Token));
        await invitations.AcceptAsync(secondPlayer, grant.Token);
        Assert.True(await access.HasRoleAsync(firstPlayer, campaign.Id, CampaignRoles.Player));
        Assert.True(await access.HasRoleAsync(secondPlayer, campaign.Id, CampaignRoles.Player));

        var afterRedemptions = await db.CampaignInvitations.AsNoTracking().SingleAsync();
        Assert.Equal(CampaignInvitationStatus.Pending, afterRedemptions.Status);
        Assert.Null(afterRedemptions.AcceptedByUserId);
        Assert.Null(afterRedemptions.AcceptedAt);

        clock.Advance(TimeSpan.FromDays(8));

        Assert.Null(await invitations.GetPreviewAsync(grant.Token));
        await Assert.ThrowsAsync<CampaignDomainException>(() => invitations.AcceptAsync(Guid.NewGuid(), grant.Token));
        var stored = await db.CampaignInvitations.AsNoTracking().SingleAsync();
        Assert.True(stored.IsReusable);
        Assert.Equal(CampaignInvitationStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task ListingPendingInvitesPersistsExpirationState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>().UseSqlite(connection).Options;
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
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
