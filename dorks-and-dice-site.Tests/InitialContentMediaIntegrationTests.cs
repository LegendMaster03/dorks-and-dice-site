using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using Microsoft.Data.Sqlite;

namespace dorks_and_dice_site.Tests;

public sealed class InitialContentMediaIntegrationTests
{
    [Fact]
    public void CommittedLocalWorkspaceIsEmptyAfterInitialContentPromotion()
    {
        using var connection = new SqliteConnection($"Data Source={GetDatabasePath()};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM content_page),
                (SELECT COUNT(*) FROM content_asset),
                (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='content_page_asset_dependency')
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
    }

    [Fact]
    public void PublicModesAllowManagedMediaRequestsToReachVisibilityEnforcement()
    {
        const string path = "/content/media/0123456789abcdef0123456789abcdef/image.png";
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Professional));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.DorksAndDice));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Unassigned));
    }

    private static string GetDatabasePath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "dorks-and-dice-site", "Content", "content.db"));
}
