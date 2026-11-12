namespace dorks_and_dice_site.Modes.DorksAndDice.Characters;

/// <summary>
/// Stable Site-to-Character-Tool route contract. The Site owns character identity and
/// campaign association; the Character Sheet tool owns the rich character sheet and
/// creation workflow attached to that identity.
/// </summary>
public static class CharacterToolContract
{
    public const string Slug = "character-sheet";
    public const string DisplayName = "Character Sheet";
    public const string RootPath = "/tools/character-sheet";
    public const string CharacterIndexPath = "/characters";
    public const string NewCharacterPath = "/tools/character-sheet/new";

    public static string CharacterPath(Guid characterId) =>
        $"/tools/{Slug}/characters/{characterId:D}";

    /// <summary>
    /// Restricted campaign-DM review route for an active connected character.
    /// The route is a navigation hint, not delegated DM authorization.
    /// Character Sheet verifies fresh campaign role and active association.
    /// </summary>
    public static string DmOriginReviewPath(Guid campaignId, Guid characterId)
    {
        if (campaignId == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("DM Origin review requires valid campaign and character IDs.");

        return $"{RootPath}/dm-origin-review/{campaignId:D}/{characterId:D}";
    }
}
