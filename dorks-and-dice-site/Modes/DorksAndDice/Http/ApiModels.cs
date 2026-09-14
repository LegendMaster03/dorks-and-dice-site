using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;

namespace dorks_and_dice_site.Modes.DorksAndDice.Http;

public sealed record CreateCampaignRequest(string Name);
public sealed record AddCampaignMemberRequest(Guid UserId, IReadOnlyCollection<string> Roles);
public sealed record SetCampaignMemberRolesRequest(IReadOnlyCollection<string> Roles);
public sealed record AddCampaignParticipantRequest(string DisplayName);
public sealed record LinkCampaignParticipantRequest(Guid UserId);
public sealed record CreateCharacterRequest(string Name);
public sealed record ConnectCharacterRequest(Guid CampaignId);

public sealed record CampaignSummaryResponse(Guid Id, string Name, IReadOnlyCollection<string> Roles);
public sealed record CampaignMemberResponse(Guid UserId, IReadOnlyCollection<string> Roles);
public sealed record CampaignParticipantResponse(Guid Id, string DisplayName, Guid? UserId, string Status);
public sealed record CampaignDetailsResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<string> CurrentUserRoles,
    IReadOnlyCollection<CampaignMemberResponse> Members,
    IReadOnlyCollection<CampaignParticipantResponse> Participants);
public sealed record CharacterCampaignResponse(Guid CampaignId, string CampaignName);
public sealed record CharacterSummaryResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<CharacterCampaignResponse> Campaigns);

public static class DorksAndDiceApiMapper
{
    public static CampaignSummaryResponse ToSummary(Campaign campaign, Guid userId)
    {
        var roles = campaign.Memberships
            .Where(membership => membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active)
            .SelectMany(membership => membership.Roles)
            .Select(role => role.Role)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        return new CampaignSummaryResponse(campaign.Id, campaign.Name, roles);
    }

    public static CampaignDetailsResponse ToDetails(Campaign campaign, Guid userId)
    {
        var currentRoles = campaign.Memberships
            .Where(membership => membership.UserId == userId
                && membership.Status == CampaignMembershipStatus.Active)
            .SelectMany(membership => membership.Roles)
            .Select(role => role.Role)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        var members = campaign.Memberships
            .Where(membership => membership.Status == CampaignMembershipStatus.Active)
            .OrderBy(membership => membership.JoinedAt)
            .Select(membership => new CampaignMemberResponse(
                membership.UserId,
                membership.Roles.Select(role => role.Role)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

        var participants = campaign.Participants
            .OrderBy(participant => participant.Status)
            .ThenBy(participant => participant.DisplayName)
            .Select(ToParticipant)
            .ToArray();

        return new CampaignDetailsResponse(campaign.Id, campaign.Name, currentRoles, members, participants);
    }

    public static CampaignParticipantResponse ToParticipant(CampaignParticipant participant)
    {
        return new CampaignParticipantResponse(
            participant.Id,
            participant.DisplayName,
            participant.UserId,
            participant.Status.ToString());
    }

    public static CharacterSummaryResponse ToSummary(Character character)
    {
        var campaigns = character.CampaignAssociations
            .Where(association => association.Status == CampaignCharacterAssociationStatus.Active)
            .OrderBy(association => association.Campaign.Name)
            .Select(association => new CharacterCampaignResponse(
                association.CampaignId,
                association.Campaign.Name))
            .ToArray();

        return new CharacterSummaryResponse(character.Id, character.Name, campaigns);
    }
}
