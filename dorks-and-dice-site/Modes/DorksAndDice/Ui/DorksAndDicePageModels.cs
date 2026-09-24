namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

public sealed record CampaignListItemViewModel(Guid Id, string Name, IReadOnlyList<string> Roles);
public sealed class CampaignsIndexViewModel
{
    public IReadOnlyList<CampaignListItemViewModel> Campaigns { get; init; } = [];
    public IReadOnlyList<CampaignListItemViewModel> ArchivedCampaigns { get; init; } = [];
}

public sealed record CampaignMemberViewModel(Guid UserId, string? ParticipantName, IReadOnlyList<string> Roles, bool IsCurrentUser);
public sealed record CampaignParticipantViewModel(Guid Id, string DisplayName, Guid? UserId, bool IsActive);
public sealed record CampaignInvitationListItemViewModel(Guid Id, IReadOnlyList<string> Roles, string? ParticipantName, DateTimeOffset ExpiresAt);
public sealed record ParticipantOptionViewModel(Guid Id, string DisplayName, bool IsFormer);

public sealed class CampaignDetailsViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public Guid CurrentUserId { get; init; }
    public bool CanManage { get; init; }
    public IReadOnlyList<string> CurrentUserRoles { get; init; } = [];
    public string? DedicatedDiscordGuildId { get; init; }
    public string? DiscordBotInstallUrl { get; init; }
    public IReadOnlyList<CampaignMemberViewModel> Members { get; init; } = [];
    public IReadOnlyList<CampaignParticipantViewModel> Participants { get; init; } = [];
    public IReadOnlyList<CampaignInvitationListItemViewModel> Invitations { get; init; } = [];
    public IReadOnlyList<ParticipantOptionViewModel> InviteableParticipants { get; init; } = [];
}

public sealed class CampaignInvitationPageViewModel
{
    public string Token { get; init; } = string.Empty;
    public Guid CampaignId { get; init; }
    public string CampaignName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string? ParticipantName { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string? Error { get; init; }
}

public sealed record CharacterCampaignConnectionViewModel(Guid CampaignId, string CampaignName);
public sealed record CharacterListItemViewModel(
    Guid Id,
    string Name,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<CharacterCampaignConnectionViewModel> ActiveCampaigns);
public sealed record CampaignOptionViewModel(Guid Id, string Name);

public sealed class CharactersIndexViewModel
{
    public IReadOnlyList<CharacterListItemViewModel> ActiveCharacters { get; init; } = [];
    public IReadOnlyList<CharacterListItemViewModel> ArchivedCharacters { get; init; } = [];
    public IReadOnlyList<CampaignOptionViewModel> PlayerCampaigns { get; init; } = [];
}
