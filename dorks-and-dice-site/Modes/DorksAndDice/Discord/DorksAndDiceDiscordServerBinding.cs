namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public enum DiscordServerCampaignScope
{
    SingleCampaign = 0,
    SelectedCampaigns = 1,
    AllDmCampaigns = 2
}

public sealed class DorksAndDiceDiscordServerBinding
{
    public const int GuildIdMaxLength = 32;

    public Guid Id { get; set; }
    public string GuildId { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public DiscordServerCampaignScope CampaignScope { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<DorksAndDiceDiscordServerCampaign> Campaigns { get; set; } =
        new List<DorksAndDiceDiscordServerCampaign>();
}

public sealed class DorksAndDiceDiscordServerCampaign
{
    public Guid BindingId { get; set; }
    public Guid CampaignId { get; set; }

    public DorksAndDiceDiscordServerBinding Binding { get; set; } = null!;
    public Campaigns.Campaign Campaign { get; set; } = null!;
}
