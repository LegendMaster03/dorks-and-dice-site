namespace dorks_and_dice_site.Models.Operator;

public sealed class OperatorAuditRecord
{
    public const int ClientMaxLength = 100;
    public const int CapabilityMaxLength = 160;
    public const int ResourceMaxLength = 512;
    public const int OutcomeMaxLength = 40;

    public Guid Id { get; set; }
    public Guid InvocationId { get; set; }
    public Guid UserId { get; set; }
    public Guid CredentialId { get; set; }
    public string Client { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Outcome { get; set; } = "Started";
}
