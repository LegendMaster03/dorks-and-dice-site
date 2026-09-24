using dorks_and_dice_site.Modes.DorksAndDice.Discord;

namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

public sealed record DiscordServerCampaignOptionViewModel(Guid Id, string Name);

public sealed record DiscordServerBindingViewModel(
    Guid Id,
    string GuildId,
    string GuildName,
    DiscordServerCampaignScope CampaignScope,
    IReadOnlyList<DiscordServerCampaignOptionViewModel> Campaigns);

public sealed class DiscordServersViewModel
{
    public string? InstallUrl { get; init; }
    public IReadOnlyList<DiscordServerCampaignOptionViewModel> EligibleCampaigns { get; init; } = [];
    public IReadOnlyList<DiscordServerBindingViewModel> Servers { get; init; } = [];
}
