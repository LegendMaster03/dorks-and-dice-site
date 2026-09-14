using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Http;

[ApiController]
[Authorize]
[Route("characters/api")]
public sealed class CharactersApiController(ICharacterService characterService) : ControllerBase
{
    private readonly ICharacterService _characterService = characterService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CharacterSummaryResponse>>> GetCharacters(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var characters = await _characterService.GetForOwnerAsync(userId, cancellationToken);
        return Ok(characters.Select(DorksAndDiceApiMapper.ToSummary).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CharacterSummaryResponse>> CreateCharacter(
        [FromBody] CreateCharacterRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var character = await _characterService.CreateAsync(userId, request.Name, cancellationToken);
            return Created(
                $"/characters/api/{character.Id}",
                DorksAndDiceApiMapper.ToSummary(character));
        }
        catch (CampaignDomainException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<CharacterSummaryResponse>> GetCharacter(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var character = await _characterService.GetAsync(userId, characterId, cancellationToken);
        return character is null
            ? NotFound()
            : Ok(DorksAndDiceApiMapper.ToSummary(character));
    }

    [HttpPost("{characterId:guid}/campaign")]
    public async Task<IActionResult> ConnectToCampaign(
        Guid characterId,
        [FromBody] ConnectCharacterRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _characterService.ConnectToCampaignAsync(
                userId,
                characterId,
                request.CampaignId,
                cancellationToken);
            return NoContent();
        }
        catch (CampaignDomainException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{characterId:guid}/campaign/{campaignId:guid}")]
    public async Task<IActionResult> DisconnectFromCampaign(
        Guid characterId,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _characterService.DisconnectFromCampaignAsync(
                userId,
                characterId,
                campaignId,
                cancellationToken);
            return NoContent();
        }
        catch (CampaignDomainException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
