using System.Data;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Authorization;

public enum ProtectedAdmissionStatus
{
    Admitted, Reused, Denied, PendingRevocation, OperationIdConflict
}

public sealed record ProtectedAdmissionResult(
    ProtectedAdmissionStatus Status, Guid OperationId, IReadOnlyList<string> ScopeKeys);

public enum PendingRevocationStatus { Recorded, Reused, ConflictingRevocation, RevocationIdConflict }

public sealed record PendingRevocationResult(
    PendingRevocationStatus Status, Guid RevocationId);

/// <summary>
/// Durable, Site-database-only serialization of protected-operation admission
/// against a pending revocation. THIS IS AN INTERNAL FOUNDATION, NOT AN R3
/// AUTHORIZATION CAPABILITY. Tool registration and account identity are in
/// separate storage domains, so callers must not expose these operations as
/// production commit authority until all grant/revocation paths are gated and
/// Character Sheet supports durably confirmed terminal outcomes.
/// </summary>
public sealed class ProtectedMutationCoordinator(DorksAndDiceDbContext db, TimeProvider clock)
{
    private const string ToolScope = "tool:character-sheet";

    public static IReadOnlyList<string> OwnerScopes(Guid actorId, Guid characterId) =>
        NormalizeScopes([
            ToolScope, $"account:{actorId:N}", $"character:{characterId:N}"
        ]);

    public static IReadOnlyList<string> DmScopes(
        Guid actorId, Guid characterId, Guid campaignId) =>
        NormalizeScopes([
            ToolScope, $"account:{actorId:N}", $"character:{characterId:N}",
            $"campaign:{campaignId:N}", $"dm:{campaignId:N}:{actorId:N}",
            $"association:{campaignId:N}:{characterId:N}"
        ]);

    public static string CharacterScope(Guid characterId) => $"character:{characterId:N}";
    public static string CampaignScope(Guid campaignId) => $"campaign:{campaignId:N}";
    public static string DmScope(Guid campaignId, Guid actorId) =>
        $"dm:{campaignId:N}:{actorId:N}";
    public static string AssociationScope(Guid campaignId, Guid characterId) =>
        $"association:{campaignId:N}:{characterId:N}";
    public static string AccountScope(Guid actorId) => $"account:{actorId:N}";
    public static string GlobalToolScope => ToolScope;

    /// <summary>
    /// Validates the current owner record INSIDE the gate transaction. The
    /// upstream Tool/identity authority is deliberately not checked here yet.
    /// </summary>
    public Task<ProtectedAdmissionResult> AdmitOwnerAsync(
        Guid operationId, Guid actorId, Guid characterId,
        string operationKind, string commandDigest, CancellationToken ct = default) =>
        AdmitAsync(operationId, actorId, characterId, null, operationKind,
            commandDigest, OwnerScopes(actorId, characterId),
            () => db.Characters.AsNoTracking().AnyAsync(character =>
                character.Id == characterId &&
                character.OwnerUserId == actorId &&
                character.Status == CharacterStatus.Active &&
                character.ArchivedAt == null, ct), ct);

    /// <summary>
    /// Serializes current campaign, membership, DM role and association
    /// verification in one relational Site query after acquiring scope gates.
    /// </summary>
    public Task<ProtectedAdmissionResult> AdmitDmAsync(
        Guid operationId, Guid actorId, Guid characterId, Guid campaignId,
        string operationKind, string commandDigest, CancellationToken ct = default) =>
        AdmitAsync(operationId, actorId, characterId, campaignId, operationKind,
            commandDigest, DmScopes(actorId, characterId, campaignId),
            () => db.CampaignCharacters.AsNoTracking().AnyAsync(association =>
                association.CampaignId == campaignId &&
                association.CharacterId == characterId &&
                association.Status == CampaignCharacterAssociationStatus.Active &&
                association.Campaign.Status == CampaignStatus.Active &&
                association.Character.Status == CharacterStatus.Active &&
                association.Character.ArchivedAt == null &&
                db.CampaignMemberships.Any(membership =>
                    membership.CampaignId == campaignId &&
                    membership.UserId == actorId &&
                    membership.Status == CampaignMembershipStatus.Active &&
                    membership.Roles.Any(role => role.Role == CampaignRoles.Dm)),
                ct), ct);

    private async Task<ProtectedAdmissionResult> AdmitAsync(
        Guid operationId, Guid actorId, Guid characterId, Guid? campaignId,
        string operationKind, string commandDigest, IReadOnlyList<string> scopeKeys,
        Func<Task<bool>> currentSiteAuthorization, CancellationToken ct)
    {
        ValidateOperation(operationId, actorId, characterId, campaignId,
            operationKind, commandDigest);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var gates = await LockGatesAsync(scopeKeys, ct);

        // Reuse must be evaluated under the same gate locks as the original
        // admission. A changed digest/actor/scope is never a legitimate retry.
        var earlier = await db.ProtectedMutationIntents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (earlier is not null)
        {
            var oldScopes = await db.ProtectedMutationIntentScopes.AsNoTracking()
                .Where(item => item.OperationId == operationId)
                .Select(item => item.ScopeKey).OrderBy(key => key).ToArrayAsync(ct);
            var matches = earlier.ActorUserId == actorId &&
                earlier.CharacterId == characterId &&
                earlier.CampaignId == campaignId &&
                earlier.OperationKind == operationKind &&
                earlier.CommandDigest == commandDigest &&
                oldScopes.SequenceEqual(scopeKeys, StringComparer.Ordinal);
            return new(matches ? ProtectedAdmissionStatus.Reused
                : ProtectedAdmissionStatus.OperationIdConflict,
                operationId, matches ? oldScopes : []);
        }

        if (gates.Any(item => item.PendingRevocationId is not null))
            return new(ProtectedAdmissionStatus.PendingRevocation, operationId, []);
        if (!await currentSiteAuthorization())
            return new(ProtectedAdmissionStatus.Denied, operationId, []);

        db.ProtectedMutationIntents.Add(new ProtectedMutationIntent
        {
            OperationId = operationId,
            ActorUserId = actorId,
            CharacterId = characterId,
            CampaignId = campaignId,
            OperationKind = operationKind,
            CommandDigest = commandDigest,
            Status = ProtectedMutationIntentStatus.Admitted,
            AdmittedAt = clock.GetUtcNow()
        });
        foreach (var gate in gates)
        {
            db.ProtectedMutationIntentScopes.Add(new ProtectedMutationIntentScope
            {
                OperationId = operationId,
                ScopeKey = gate.ScopeKey,
                AdmittedGeneration = gate.Generation
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(ProtectedAdmissionStatus.Admitted, operationId, scopeKeys);
    }

    /// <summary>
    /// Records a durable pending barrier. The caller must determine the
    /// affected scopes from authoritative Site data and authenticate the
    /// initiator. This phase intentionally DOES NOT settle or apply revocation:
    /// terminal Sheet receipts, Tool/Identity changes and final acknowledgement
    /// require the remaining cross-service protocol.
    /// </summary>
    public async Task<PendingRevocationResult> BeginPendingRevocationAsync(
        Guid revocationId, Guid initiatorId, string changeKind, Guid subjectId,
        IReadOnlyCollection<string> affectedScopes, CancellationToken ct = default)
    {
        if (revocationId == Guid.Empty || initiatorId == Guid.Empty ||
            subjectId == Guid.Empty || string.IsNullOrWhiteSpace(changeKind) ||
            changeKind.Length > 80)
            throw new ArgumentException("Invalid revocation request.");
        var keys = NormalizeScopes(affectedScopes);
        if (keys.Count == 0)
            throw new ArgumentException("A revocation must affect at least one scope.");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var gates = await LockGatesAsync(keys, ct);

        var existing = await db.ProtectedMutationRevocations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RevocationId == revocationId, ct);
        if (existing is not null)
        {
            var recorded = await db.ProtectedMutationRevocationScopes.AsNoTracking()
                .Where(item => item.RevocationId == revocationId)
                .Select(item => item.ScopeKey).OrderBy(key => key).ToArrayAsync(ct);
            var matches = existing.InitiatorUserId == initiatorId &&
                existing.SubjectId == subjectId && existing.ChangeKind == changeKind &&
                recorded.SequenceEqual(keys, StringComparer.Ordinal);
            return new(matches ? PendingRevocationStatus.Reused
                : PendingRevocationStatus.RevocationIdConflict, revocationId);
        }
        if (gates.Any(item => item.PendingRevocationId is not null))
            return new(PendingRevocationStatus.ConflictingRevocation, revocationId);

        db.ProtectedMutationRevocations.Add(new ProtectedMutationRevocation
        {
            RevocationId = revocationId,
            InitiatorUserId = initiatorId,
            SubjectId = subjectId,
            ChangeKind = changeKind,
            Status = ProtectedMutationRevocationStatus.Pending,
            RequestedAt = clock.GetUtcNow()
        });
        foreach (var gate in gates)
        {
            db.ProtectedMutationRevocationScopes.Add(new ProtectedMutationRevocationScope
            {
                RevocationId = revocationId, ScopeKey = gate.ScopeKey
            });
            // Gates are queried without tracking so a partial request cannot
            // reuse a tracked stale snapshot from an earlier operation.
            var changed = await db.ProtectedMutationScopeGates.Where(item =>
                item.ScopeKey == gate.ScopeKey && item.PendingRevocationId == null)
                .ExecuteUpdateAsync(update =>
                    update.SetProperty(item => item.PendingRevocationId, revocationId), ct);
            if (changed != 1)
                throw new InvalidOperationException(
                    "A Site authorization scope changed during revocation serialization.");
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(PendingRevocationStatus.Recorded, revocationId);
    }

    /// <summary>
    /// Read-only inspection for a future recovery worker. An outstanding
    /// admission is never presumed aborted merely because Sheet is offline.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetUnresolvedOperationsAsync(
        Guid revocationId, CancellationToken ct = default)
    {
        var scopeKeys = await db.ProtectedMutationRevocationScopes.AsNoTracking()
            .Where(item => item.RevocationId == revocationId)
            .Select(item => item.ScopeKey).ToArrayAsync(ct);
        if (scopeKeys.Length == 0) return [];
        return await db.ProtectedMutationIntents.AsNoTracking()
            .Where(item => item.Status == ProtectedMutationIntentStatus.Admitted &&
                db.ProtectedMutationIntentScopes.Any(scope =>
                    scope.OperationId == item.OperationId &&
                    scopeKeys.Contains(scope.ScopeKey)))
            .Select(item => item.OperationId)
            .Distinct().OrderBy(id => id).ToArrayAsync(ct);
    }

    private async Task<IReadOnlyList<ProtectedMutationScopeGate>> LockGatesAsync(
        IReadOnlyList<string> keys, CancellationToken ct)
    {
        var locked = new List<ProtectedMutationScopeGate>(keys.Count);
        foreach (var key in keys)
        {
            // The unique-key insert races safely with other transactions
            // creating the same gate. The following PostgreSQL row lock is
            // held until admission or revocation has committed.
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dd_protected_mutation_scope (\"ScopeKey\", \"Generation\") VALUES ({key}, 0) ON CONFLICT (\"ScopeKey\") DO NOTHING",
                    ct);
                var gate = await db.ProtectedMutationScopeGates
                    .FromSqlInterpolated(
                        $"SELECT * FROM dd_protected_mutation_scope WHERE \"ScopeKey\" = {key} FOR UPDATE")
                    .AsNoTracking().SingleAsync(ct);
                locked.Add(gate);
            }
            else if (db.Database.IsSqlite())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT OR IGNORE INTO dd_protected_mutation_scope (\"ScopeKey\", \"Generation\") VALUES ({key}, 0)",
                    ct);
                locked.Add(await db.ProtectedMutationScopeGates
                    .AsNoTracking().SingleAsync(item => item.ScopeKey == key, ct));
            }
            else throw new NotSupportedException("Unknown Site coordinator storage provider.");
        }
        return locked;
    }

    private static IReadOnlyList<string> NormalizeScopes(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var sorted = scopes.Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (sorted.Any(key => string.IsNullOrWhiteSpace(key) ||
            key.Length > 180 || key.Any(ch =>
                !(char.IsAsciiLetterOrDigit(ch) || ch is ':' or '-'))))
            throw new ArgumentException("Invalid Site authorization scope.");
        return sorted;
    }

    private static void ValidateOperation(
        Guid opId, Guid actorId, Guid characterId, Guid? campaignId,
        string kind, string digest)
    {
        if (opId == Guid.Empty || actorId == Guid.Empty ||
            characterId == Guid.Empty || campaignId == Guid.Empty ||
            string.IsNullOrWhiteSpace(kind) || kind.Length > 80 ||
            digest is null || digest.Length != 64 ||
            digest.Any(ch => ch is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid protected-operation identity.");
    }
}
