using dorks_and_dice_site.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class IdentityConnectionStringResolverTests
{
    [Fact]
    public void RelativeSqliteIdentityPathIsAnchoredToContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"identity-root-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityStorage:Provider"] = "Sqlite",
                ["ConnectionStrings:IdentityDatabase"] = "Data Source=Content/identity-development.db"
            })
            .Build();

        var resolved = IdentityConnectionStringResolver.Resolve(configuration, contentRoot);
        var sqlite = new SqliteConnectionStringBuilder(resolved);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "Content", "identity-development.db")),
            sqlite.DataSource);
    }
}
