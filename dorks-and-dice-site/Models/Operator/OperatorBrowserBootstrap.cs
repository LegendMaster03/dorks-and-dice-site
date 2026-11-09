namespace dorks_and_dice_site.Models.Operator;

/// <summary>
/// One-use, short-lived operator browser-session exchange record. Its
/// persistent verifier is a hash; the bearer secret is not stored.
/// </summary>
public sealed class OperatorBrowserBootstrap
{
    public const int SecretHashMaxLength = 64;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid IssuanceInvocationId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
