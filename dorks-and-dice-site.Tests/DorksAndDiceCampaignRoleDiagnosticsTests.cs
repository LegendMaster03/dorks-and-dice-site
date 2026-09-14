using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceCampaignRoleDiagnosticsTests
{
    [Fact]
    public async Task ReportConcurrencyEntityForRolePromotion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new DorksAndDiceDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var access = new CampaignAccessService(db);
        var campaigns = new CampaignService(db, access, TimeProvider.System);
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await campaigns.CreateAsync(dm, "Campaign");
        await campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);

        try
        {
            await campaigns.SetMemberRolesAsync(dm, campaign.Id, player, [CampaignRoles.Player, CampaignRoles.Dm]);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entries = string.Join(", ", exception.Entries.Select(entry =>
                $"{entry.Metadata.ClrType.Name}:{entry.State}:" +
                string.Join("|", entry.Properties.Where(property => property.Metadata.IsPrimaryKey())
                    .Select(property => property.CurrentValue?.ToString() ?? "null"))));
            Assert.Fail($"Concurrency entries: {entries}");
        }
    }
}
