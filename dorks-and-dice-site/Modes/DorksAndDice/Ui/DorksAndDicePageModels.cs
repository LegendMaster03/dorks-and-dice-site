namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

public sealed record CampaignListItemViewModel(
    Guid Id,
    string Name,
    IReadOnlyList<string> Roles);

public sealed class CampaignsIndexViewModel
{
    public IReadOnlyList<CampaignListItemViewModel> Campaigns { get; init; } = [];
}

public sealed record CampaignMemberViewModel(
    Guid UserId,
    IReadOnlyList<string> Roles,
    bool IsCurrentUser);

public sealed record CampaignParticipantViewModel(
    Guid Id,
    string DisplayName,
    Guid? UserId,
    bool IsActive);

public sealed class CampaignDetailsViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid CurrentUserId { get; init; }
    public bool CanManage { get; init; }
    public IReadOnlyList<string> CurrentUserRoles { get; init; } = [];
    public IReadOnlyList<CampaignMemberViewModel> Members { get; init; } = [];
    public IReadOnlyList<CampaignParticipantViewModel> Participants { get; init; } = [];
}

public sealed record CharacterListItemViewModel(
    Guid Id,
    string Name,
    Guid? ActiveCampaignId,
    string? ActiveCampaignName);

public sealed record CampaignOptionViewModel(Guid Id, string Name);

public sealed class CharactersIndexViewModel
{
    public IReadOnlyList<CharacterListItemViewModel> Characters { get; init; } = [];
    public IReadOnlyList<CampaignOptionViewModel> PlayerCampaigns { get; init; } = [];
}
