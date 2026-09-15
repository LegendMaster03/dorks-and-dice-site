using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Http;

[ApiController]
[Authorize]
public sealed class PermanentDeletionApiController(IDorksAndDiceDeletionService deletionService) : ControllerBase
{
    private readonly IDorksAndDiceDeletionService _deletionService = deletionService;

    [HttpDelete("/characters/api/{characterId:guid}")]
    public async Task<IActionResult> DeleteCharacter(Guid characterId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _deletionService.DeleteCharacterAsync(userId, characterId, cancellationToken);
            return NoContent();
        }
        catch (CampaignDomainException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("/campaigns/api/{campaignId:guid}")]
    public async Task<IActionResult> DeleteCampaign(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _deletionService.DeleteCampaignAsync(userId, campaignId, cancellationToken);
            return NoContent();
        }
        catch (CampaignDomainException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
