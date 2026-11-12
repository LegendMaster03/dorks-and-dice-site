using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Ui;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// Site navigation is restricted to the current active DM role and active
/// campaign context. The character Tool still verifies permission on entry.
/// </summary>
public sealed class DorksAndDiceCampaignOriginReviewNavigationTests
{
    private static readonly Guid CampaignId = Guid.Parse("3e50b027-9c43-401f-bb31-ae4ae7409ce4");
    private static readonly Guid CharacterId = Guid.Parse("8f62ed58-0f5f-4e71-9a18-8d9dcfe71dc7");
    private static readonly Guid OtherId = Guid.Parse("9c97ec43-39bd-45d9-9264-64387d0f6cb6");

    private static CampaignContextSnapshot Context(
        IReadOnlyList<string> roles, Guid? campaignId = null,
        IReadOnlyList<CampaignCharacterContext>? characters = null) =>
        new(campaignId ?? CampaignId, "Campaign", roles, [],
            characters ?? [new(CharacterId, Guid.NewGuid(), "Elf character")]);

    [Fact]
    public void ActiveDmHasNavigationToExactCampaignCharacter()
    {
        var targets = CampaignOriginReviewNavigation.FromSiteContext(
            Context([CampaignRoles.Dm]), CampaignId);
        var character = Assert.Single(targets);
        Assert.Equal(CharacterId, character.CharacterId);
        Assert.Equal("Elf character", character.Name);
        Assert.Equal(
            $"/tools/character-sheet/dm-origin-review/{CampaignId:D}/{CharacterId:D}",
            CharacterToolContract.DmOriginReviewPath(CampaignId, character.CharacterId));
    }

    [Fact]
    public void PlayerAndMismatchedContextNeverGetDmReviewLinks()
    {
        Assert.Empty(CampaignOriginReviewNavigation.FromSiteContext(
            Context([CampaignRoles.Player]), CampaignId));
        Assert.Empty(CampaignOriginReviewNavigation.FromSiteContext(
            Context([]), CampaignId));
        Assert.Empty(CampaignOriginReviewNavigation.FromSiteContext(
            Context([CampaignRoles.Dm], OtherId), CampaignId));
        Assert.Empty(CampaignOriginReviewNavigation.FromSiteContext(
            null, CampaignId));
        Assert.Empty(CampaignOriginReviewNavigation.FromSiteContext(
            Context([CampaignRoles.Dm]), Guid.Empty));
    }

    [Fact]
    public void NavigationListsOnlyDistinctNonemptyActiveContextCharacters()
    {
        var targets = CampaignOriginReviewNavigation.FromSiteContext(
            Context([CampaignRoles.Dm],
                characters: [
                    new(OtherId, Guid.NewGuid(), "Zed"),
                    new(CharacterId, Guid.NewGuid(), "Ada"),
                    new(CharacterId, Guid.NewGuid(), "Ada duplicate"),
                    new(Guid.Empty, Guid.NewGuid(), "Invalid"),
                    new(Guid.NewGuid(), Guid.NewGuid(), " ")
                ]), CampaignId);
        Assert.Equal(2, targets.Count);
        Assert.Equal("Ada", targets[0].Name);
        Assert.Equal(CharacterId, targets[0].CharacterId);
        Assert.Equal(OtherId, targets[1].CharacterId);
    }

    [Fact]
    public void ReviewRouteRejectsEmptyAuthorityIdentifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterToolContract.DmOriginReviewPath(Guid.Empty, CharacterId));
        Assert.Throws<ArgumentException>(() =>
            CharacterToolContract.DmOriginReviewPath(CampaignId, Guid.Empty));
    }
}
