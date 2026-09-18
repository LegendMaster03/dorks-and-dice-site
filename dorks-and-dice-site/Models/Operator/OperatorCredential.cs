namespace dorks_and_dice_site.Models.Operator;

public sealed class OperatorCredential
{
    public const int NameMaxLength = 100;
    public const int SecretHashMaxLength = 64;
    public const int SecretPrefixMaxLength = 48;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string SecretPrefix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
