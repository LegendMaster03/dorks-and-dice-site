namespace dorks_and_dice_site.Models.Identity;

public sealed class AccountLinkModeActivation
{
    public const int ModeIdMaxLength = 100;
    public const int ProviderIdMaxLength = 100;
    public const int ResourceIdMaxLength = 256;

    public Guid UserId { get; set; }
    public string ModeId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
}
