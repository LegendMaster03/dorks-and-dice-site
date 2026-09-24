namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public sealed class CampaignDiscordGuildBinding
{
    public const int GuildIdMaxLength = 32;

    public Guid CampaignId { get; set; }
    public string GuildId { get; set; } = string.Empty;
    public Guid ConfiguredByUserId { get; set; }
    public DateTimeOffset ConfiguredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
