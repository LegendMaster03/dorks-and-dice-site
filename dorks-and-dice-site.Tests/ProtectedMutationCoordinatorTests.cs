using dorks_and_dice_site.Modes.DorksAndDice.Authorization;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// R3 phase-one durable Site journal tests. No endpoint or production
/// cross-service authorization is enabled by this foundation. All admission
/// is still missing the external Tool/Identity registration fence and the
/// Character Sheet terminal commit/abort protocol.
/// </summary>
public sealed class ProtectedMutationCoordinatorTests
{
    private static readonly string Digest = new('a', 64);

    [Fact]
    public async Task PendingRevocationBlocksNewAdmissionButPreservesEarlierOperation()
    {
        await using var harness = await Harness.CreateAsync();
        var owner = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await harness.AddCharacterAsync(owner, firstId);
        await harness.AddCharacterAsync(owner, secondId);

        var oldOp = Guid.NewGuid();
        var admitted = await harness.Coordinator.AdmitOwnerAsync(
            oldOp, owner, firstId, "initial-origin", Digest);
        Assert.Equal(ProtectedAdmissionStatus.Admitted, admitted.Status);

        var pendingId = Guid.NewGuid();
        var pending = await harness.Coordinator.BeginPendingRevocationAsync(
            pendingId, owner, "character-archive", firstId,
            [ProtectedMutationCoordinator.CharacterScope(firstId)]);
        Assert.Equal(PendingRevocationStatus.Recorded, pending.Status);

        Assert.Equal(ProtectedAdmissionStatus.PendingRevocation,
            (await harness.Coordinator.AdmitOwnerAsync(
                Guid.NewGuid(), owner, firstId, "initial-origin", Digest)).Status);
        Assert.Equal(ProtectedAdmissionStatus.Reused,
            (await harness.Coordinator.AdmitOwnerAsync(
                oldOp, owner, firstId, "initial-origin", Digest)).Status);

        Assert.Equal(ProtectedAdmissionStatus.Admitted,
            (await harness.Coordinator.AdmitOwnerAsync(
                Guid.NewGuid(), owner, secondId, "initial-origin", Digest)).Status);
        Assert.Contains(oldOp, await harness.Coordinator.GetUnresolvedOperationsAsync(pendingId));
        var stored = await harness.Db.ProtectedMutationRevocations.AsNoTracking()
            .SingleAsync(item => item.RevocationId == pendingId);
        Assert.Equal(ProtectedMutationRevocationStatus.Pending, stored.Status);
        Assert.Null(stored.EffectiveAt);

        // Beginning a pending revocation does NOT silently archive the
        // character or claim that authority was already revoked.
        Assert.Equal(CharacterStatus.Active,
            (await harness.Db.Characters.SingleAsync(item => item.Id == firstId)).Status);
    }

    [Fact]
    public async Task DuplicateOperationsAndRevocationsAreIdempotentButNeverSubstitutable()
    {
        await using var harness = await Harness.CreateAsync();
        var owner = Guid.NewGuid();
        var character = Guid.NewGuid();
        await harness.AddCharacterAsync(owner, character);
        var operationId = Guid.NewGuid();

        Assert.Equal(ProtectedAdmissionStatus.Admitted,
            (await harness.Coordinator.AdmitOwnerAsync(
                operationId, owner, character, "transition", Digest)).Status);
        Assert.Equal(ProtectedAdmissionStatus.OperationIdConflict,
            (await harness.Coordinator.AdmitOwnerAsync(
                operationId, owner, character, "transition", new string('b', 64))).Status);
        Assert.Equal(ProtectedAdmissionStatus.OperationIdConflict,
            (await harness.Coordinator.AdmitOwnerAsync(
                operationId, Guid.NewGuid(), character, "transition", Digest)).Status);
        Assert.Single(await harness.Db.ProtectedMutationIntents.ToArrayAsync());

        var request = Guid.NewGuid();
        var scopes = new[] { ProtectedMutationCoordinator.CharacterScope(character) };
        Assert.Equal(PendingRevocationStatus.Recorded,
            (await harness.Coordinator.BeginPendingRevocationAsync(
                request, owner, "archive", character, scopes)).Status);
        Assert.Equal(PendingRevocationStatus.Reused,
            (await harness.Coordinator.BeginPendingRevocationAsync(
                request, owner, "archive", character, scopes)).Status);
        Assert.Equal(PendingRevocationStatus.RevocationIdConflict,
            (await harness.Coordinator.BeginPendingRevocationAsync(
                request, owner, "remove", character, scopes)).Status);
        Assert.Equal(PendingRevocationStatus.ConflictingRevocation,
            (await harness.Coordinator.BeginPendingRevocationAsync(
                Guid.NewGuid(), owner, "archive", character, scopes)).Status);
        Assert.Single(await harness.Db.ProtectedMutationRevocations.ToArrayAsync());
    }

    [Fact]
    public async Task LiveOwnerAndDmPredicatesRejectOutOfScopeOrArchivedAdmissions()
    {
        await using var harness = await Harness.CreateAsync();
        var dm = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = await new CampaignService(harness.Db,
            new CampaignAccessService(harness.Db), TimeProvider.System)
            .CreateAsync(dm, "Arcs of Fate");
        var campaigns = new CampaignService(harness.Db,
            new CampaignAccessService(harness.Db), TimeProvider.System);
        await campaigns.AddMemberAsync(dm, campaign.Id, player, [CampaignRoles.Player]);

        var charId = Guid.NewGuid();
        var characterService = new CharacterService(harness.Db,
            new CampaignAccessService(harness.Db), TimeProvider.System);
        await characterService.CreateAsync(player, "First");
        // The precise target record is explicitly owned by the player.
        await harness.AddCharacterAsync(player, charId);
        await characterService.ConnectToCampaignAsync(player, charId, campaign.Id);

        Assert.Equal(ProtectedAdmissionStatus.Denied,
            (await harness.Coordinator.AdmitOwnerAsync(
                Guid.NewGuid(), dm, charId, "initial-origin", Digest)).Status);
        Assert.Equal(ProtectedAdmissionStatus.Admitted,
            (await harness.Coordinator.AdmitDmAsync(
                Guid.NewGuid(), dm, charId, campaign.Id, "mixed-origin", Digest)).Status);
        Assert.Equal(ProtectedAdmissionStatus.Denied,
            (await harness.Coordinator.AdmitDmAsync(
                Guid.NewGuid(), player, charId, campaign.Id, "mixed-origin", Digest)).Status);

        await characterService.ArchiveAsync(player, charId);
        Assert.Equal(ProtectedAdmissionStatus.Denied,
            (await harness.Coordinator.AdmitOwnerAsync(
                Guid.NewGuid(), player, charId, "initial-origin", Digest)).Status);
        Assert.Equal(ProtectedAdmissionStatus.Denied,
            (await harness.Coordinator.AdmitDmAsync(
                Guid.NewGuid(), dm, charId, campaign.Id, "mixed-origin", Digest)).Status);
    }

    [Fact]
    public async Task PendingScopeSurvivesContextRestartAndBlocksAdmission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DorksAndDiceDbContext>()
            .UseSqlite(connection).Options;
        var owner = Guid.NewGuid();
        var character = Guid.NewGuid();
        var scope = ProtectedMutationCoordinator.CharacterScope(character);
        var pendingId = Guid.NewGuid();

        await using (var setup = new DorksAndDiceDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Characters.Add(new Character
            {
                Id = character, OwnerUserId = owner, Name = "Persisted",
                Status = CharacterStatus.Active, CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await setup.SaveChangesAsync();
            Assert.Equal(PendingRevocationStatus.Recorded,
                (await new ProtectedMutationCoordinator(setup, TimeProvider.System)
                    .BeginPendingRevocationAsync(
                        pendingId, owner, "archive", character, [scope])).Status);
        }

        await using (var restarted = new DorksAndDiceDbContext(options))
        {
            var coordinator = new ProtectedMutationCoordinator(restarted, TimeProvider.System);
            Assert.Equal(ProtectedAdmissionStatus.PendingRevocation,
                (await coordinator.AdmitOwnerAsync(
                    Guid.NewGuid(), owner, character, "initial-origin", Digest)).Status);
            Assert.Equal(pendingId,
                (await restarted.ProtectedMutationScopeGates.AsNoTracking()
                    .SingleAsync(gate => gate.ScopeKey == scope)).PendingRevocationId);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private Harness(SqliteConnection connection, DorksAndDiceDbContext db)
        {
            _connection = connection;
            Db = db;
            Coordinator = new ProtectedMutationCoordinator(db, TimeProvider.System);
        }
        public DorksAndDiceDbContext Db { get; }
        public ProtectedMutationCoordinator Coordinator { get; }
        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new DorksAndDiceDbContext(
                new DbContextOptionsBuilder<DorksAndDiceDbContext>()
                    .UseSqlite(connection).Options);
            await db.Database.MigrateAsync();
            return new Harness(connection, db);
        }
        public async Task AddCharacterAsync(Guid owner, Guid id)
        {
            Db.Characters.Add(new Character
            {
                Id = id, OwnerUserId = owner, Name = "Subject",
                Status = CharacterStatus.Active, CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
