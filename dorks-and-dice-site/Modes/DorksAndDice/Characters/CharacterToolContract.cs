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
    public const string NewCharacterPath = "/tools/character-sheet/new";

    public static string CharacterPath(Guid characterId) =>
        $"/tools/{Slug}/characters/{characterId:D}";
}
