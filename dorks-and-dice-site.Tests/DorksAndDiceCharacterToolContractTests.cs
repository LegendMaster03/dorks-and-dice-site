using dorks_and_dice_site.Modes.DorksAndDice.Characters;

namespace dorks_and_dice_site.Tests;

public sealed class DorksAndDiceCharacterToolContractTests
{
    [Fact]
    public void CharacterToolUsesStableRegisteredSlugAndBuilderRoute()
    {
        Assert.Equal("character-sheet", CharacterToolContract.Slug);
        Assert.Equal("Character Sheet", CharacterToolContract.DisplayName);
        Assert.Equal("/tools/character-sheet/new", CharacterToolContract.NewCharacterPath);
    }

    [Fact]
    public void ExistingCharacterRouteCarriesStableCharacterIdentity()
    {
        var characterId = Guid.Parse("c27149c6-bc35-4fd3-9826-6f63d61f4268");

        Assert.Equal(
            "/tools/character-sheet/characters/c27149c6-bc35-4fd3-9826-6f63d61f4268",
            CharacterToolContract.CharacterPath(characterId));
    }
}
