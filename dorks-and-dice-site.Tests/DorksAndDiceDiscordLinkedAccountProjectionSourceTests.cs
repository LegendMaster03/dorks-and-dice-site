using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceDiscordLinkedAccountProjectionSourceTests
{
    [Fact]
    public async Task RulesLawyerProjectionReusesLegacyManagedDiscordRoleIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "rules-lawyer@example.test",
            NormalizedUserName = "RULES-LAWYER@EXAMPLE.TEST",
            Email = "rules-lawyer@example.test",
            NormalizedEmail = "RULES-LAWYER@EXAMPLE.TEST",
            DisplayName = "Rules Lawyer",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserLogins.Add(new IdentityUserLogin<Guid>
        {
            LoginProvider = DiscordProvider.Id,
            ProviderKey = "777",
            ProviderDisplayName = "Discord",
            UserId = userId
        });
        db.AccountLinkModeActivations.Add(new AccountLinkModeActivation
        {
            UserId = userId,
            ModeId = AccountRoleScopes.DorksAndDice,
            ProviderId = DiscordProvider.Id,
            ResourceId = "100",
            ActivatedAt = DateTimeOffset.UtcNow
        });
        db.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = userId,
            ClaimType = AccountClaimTypes.ScopedRole,
            ClaimValue = $"{AccountRoleScopes.DorksAndDice}:{ScopedAccountRoles.RulesLawyer}"
        });
        await db.SaveChangesAsync();

        var source = new DorksAndDiceDiscordLinkedAccountProjectionSource(
            db,
            new FixedModeExternalConnectionRegistry(
                new ModeExternalConnection(
                    AccountRoleScopes.DorksAndDice,
                    DiscordProvider.Id,
                    "100")));

        var projection = Assert.Single(await source.BuildAsync());
        var role = Assert.Single(projection.Roles.Where(candidate =>
            string.Equals(
                candidate.DisplayName,
                "Dorks & Dice Rules Lawyer",
                StringComparison.Ordinal)));

        Assert.Equal("site-global-rules-lawyer", role.Key);
        Assert.Contains(userId, role.UserIds);
        Assert.DoesNotContain(
            projection.Roles,
            candidate => string.Equals(
                candidate.Key,
                "site-scoped-rules-lawyer",
                StringComparison.Ordinal));
    }

    private sealed class FixedModeExternalConnectionRegistry(ModeExternalConnection connection)
        : IModeExternalConnectionRegistry
    {
        public IReadOnlyList<ModeExternalConnection> All { get; } = [connection];

        public IReadOnlyList<ModeExternalConnection> GetForMode(string modeId) =>
            string.Equals(modeId, connection.ModeId, StringComparison.Ordinal)
                ? [connection]
                : [];

        public bool TryGet(
            string modeId,
            string providerId,
            out ModeExternalConnection? result)
        {
            if (string.Equals(modeId, connection.ModeId, StringComparison.Ordinal)
                && string.Equals(providerId, connection.ProviderId, StringComparison.OrdinalIgnoreCase))
            {
                result = connection;
                return true;
            }

            result = null;
            return false;
        }
    }
}
