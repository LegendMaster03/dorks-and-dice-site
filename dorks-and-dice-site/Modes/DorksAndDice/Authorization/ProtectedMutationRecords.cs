namespace dorks_and_dice_site.Modes.DorksAndDice.Authorization;

/// <summary>
/// Site-owned durable ordering records. These are not a usable cross-service
/// authorization protocol until the Tool registry, Identity and Character
/// Sheet terminal-outcome boundaries are connected and verified.
/// </summary>
public sealed class ProtectedMutationScopeGate
{
    public string ScopeKey { get; set; } = string.Empty;
    public long Generation { get; set; }
    public Guid? PendingRevocationId { get; set; }
}

public enum ProtectedMutationIntentStatus { Admitted = 0, Committed = 1, Aborted = 2 }

public sealed class ProtectedMutationIntent
{
    public Guid OperationId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? CampaignId { get; set; }
    public string OperationKind { get; set; } = string.Empty;
    public string CommandDigest { get; set; } = string.Empty;
    public ProtectedMutationIntentStatus Status { get; set; }
    public DateTimeOffset AdmittedAt { get; set; }
    // Not populated until authenticated Sheet terminal reconciliation exists.
    public DateTimeOffset? TerminalObservedAt { get; set; }
    public string? TerminalReceiptDigest { get; set; }
}

public sealed class ProtectedMutationIntentScope
{
    public Guid OperationId { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public long AdmittedGeneration { get; set; }
}

public enum ProtectedMutationRevocationStatus { Pending = 0, Effective = 1 }

public sealed class ProtectedMutationRevocation
{
    public Guid RevocationId { get; set; }
    public Guid InitiatorUserId { get; set; }
    public string ChangeKind { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public ProtectedMutationRevocationStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    // EffectiveAt must only be set after a trusted cross-service settlement
    // AND the underlying authority mutation are durably confirmed.
    public DateTimeOffset? EffectiveAt { get; set; }
}

public sealed class ProtectedMutationRevocationScope
{
    public Guid RevocationId { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
}
