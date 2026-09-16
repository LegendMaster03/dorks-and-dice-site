namespace dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;

public static class ToolLifecycleEventTypes
{
    public const string CharacterDeleted = "character.deleted";
    public const string CampaignDeleted = "campaign.deleted";
}

public static class ToolLifecycleTargets
{
    public const string CharacterSheet = "character-sheet";
}

public sealed class ToolLifecycleOutboxEvent
{
    public Guid EventId { get; set; }
    public string TargetToolSlug { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? LastError { get; set; }
}
