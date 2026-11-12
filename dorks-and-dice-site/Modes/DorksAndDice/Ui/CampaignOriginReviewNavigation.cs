using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;

namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

/// <summary>
/// Projects Site-authorized campaign character names into DM navigation.
/// Rendering a link never authorizes a DM decision or cross-owner sheet read:
/// Character Sheet independently rechecks active DM and character scope.
/// </summary>
public static class CampaignOriginReviewNavigation
{
    public static IReadOnlyList<CampaignOriginReviewCharacterViewModel> FromSiteContext(
        CampaignContextSnapshot? context, Guid campaignId)
    {
        if (campaignId == Guid.Empty || context?.CampaignId != campaignId ||
            !(context.RequestingUserRoles?.Contains(CampaignRoles.Dm,
                StringComparer.Ordinal) ?? false))
            return [];

        // The Site context already filters for active character associations
        // and active characters. Do not join to private sheet state here.
        return context.Characters
            .Where(character => character.CharacterId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(character.Name))
            .GroupBy(character => character.CharacterId)
            .Select(group => group.First())
            .OrderBy(character => character.Name, StringComparer.Ordinal)
            .ThenBy(character => character.CharacterId)
            .Select(character => new CampaignOriginReviewCharacterViewModel(
                character.CharacterId, character.Name))
            .ToArray();
    }
}
