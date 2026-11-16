using System.Net.Http.Headers;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Http;

/// <summary>
/// Dorks & Dice-owned live character-access lookup for the Character Sheet backend.
/// A Site-issued, source-scoped server delegation capability proves the caller's
/// actor identity; character ownership, lifecycle and active associations are
/// read afresh from the Site database on every request. This is NOT a
/// cross-service transaction or a commit-bound mutation lease.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class CharacterToolFreshAccessController(
    IToolRegistry tools,
    IToolDelegationCapabilityService capabilities,
    ICharacterService characters) : ControllerBase
{
    [HttpPost("~/tool-host/registrations/{sourceKey}/api/characters/{characterId:guid}/fresh-access")]
    public async Task<IActionResult> Verify(
        string sourceKey, Guid characterId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (sourceKey != CharacterToolContract.Slug || characterId == Guid.Empty)
            return Unauthorized();

        // Never accept a browser cookie, a signed Site ticket or a forwarded
        // user-id header here. This credential is issued only to the Tool
        // backend during Site introspection and is bounded to its registration.
        var header = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase) ||
            header.Length <= bearer.Length ||
            !capabilities.TryUse(sourceKey, header[bearer.Length..].Trim(), out var context) ||
            context is null ||
            context.RegistrationKey != sourceKey ||
            context.SiteMode != SiteModeValues.DorksAndDiceModeValue ||
            !Guid.TryParse(context.User.Id, out var actor) || actor == Guid.Empty)
            return Unauthorized();

        var registration = await tools.GetByKeyAsync(sourceKey, cancellationToken);
        if (registration is null || !registration.Enabled ||
            registration.Kind != ToolKind.Application ||
            registration.Slug != CharacterToolContract.Slug ||
            !ToolVisibility.IsVisibleInMode(registration, context.SiteMode) ||
            !ToolApplicationAccessPolicy.CanAccessFromHostContext(registration, context))
            return Unauthorized();

        // This is deliberately a new authoritative database read, not
        // context.Characters from the earlier ticket introspection.
        var character = await characters.GetAsync(actor, characterId, cancellationToken);
        if (character is null)
            return NotFound();
        if (character.Status == CharacterStatus.Active && character.ArchivedAt is not null ||
            character.Status == CharacterStatus.Archived && character.ArchivedAt is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var activeCampaignIds = character.CampaignAssociations
            .Where(link => link.Status == CampaignCharacterAssociationStatus.Active)
            .Select(link => link.CampaignId).Distinct().Order().ToArray();
        return Ok(new
        {
            contractVersion = 1,
            sourceToolKey = sourceKey,
            actorUserId = actor,
            characterId = character.Id,
            characterName = character.Name,
            status = character.Status.ToString(),
            archivedAt = character.ArchivedAt,
            campaignIds = activeCampaignIds,
            allowsOrdinaryEditing = character.Status == CharacterStatus.Active
        });
    }
}
