using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    ICharacterService characters,
    DorksAndDiceDbContext db) : ControllerBase
{
    [HttpPost("~/tool-host/registrations/{sourceKey}/api/characters/{characterId:guid}/fresh-access")]
    public async Task<IActionResult> Verify(
        string sourceKey, Guid characterId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (sourceKey != CharacterToolContract.Slug || characterId == Guid.Empty)
            return Unauthorized();

        var actor = await AuthorizedBackendActorAsync(sourceKey, cancellationToken);
        if (actor is not Guid authorizedActor)
            return Unauthorized();

        // This is deliberately a new authoritative database read, not
        // context.Characters from the earlier ticket introspection.
        var character = await characters.GetAsync(authorizedActor, characterId, cancellationToken);
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
            actorUserId = authorizedActor,
            characterId = character.Id,
            characterName = character.Name,
            status = character.Status.ToString(),
            archivedAt = character.ArchivedAt,
            campaignIds = activeCampaignIds,
            allowsOrdinaryEditing = character.Status == CharacterStatus.Active
        });
    }
    /// <summary>
    /// A live DM authorization must come from the current campaign membership
    /// and active Character association, never the source ticket's captured
    /// DM scopes. The character need not belong to this DM.
    /// </summary>
    [HttpPost("~/tool-host/registrations/{sourceKey}/api/campaigns/{campaignId:guid}/characters/{characterId:guid}/fresh-dm-access")]
    public async Task<IActionResult> VerifyDm(
        string sourceKey, Guid campaignId, Guid characterId,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (campaignId == Guid.Empty || characterId == Guid.Empty)
            return NotFound();
        var actor = await AuthorizedBackendActorAsync(sourceKey, cancellationToken);
        if (actor is not Guid authorizedActor)
            return Unauthorized();

        // Resolve current DM membership, role, campaign lifecycle, and
        // character association in ONE Site database statement. The generic
        // campaign-context projection performs several separate reads; a role
        // from one moment must not combine with an association from another.
        // This is read-time consistency only, NOT a commit-spanning lease.
        var hasActiveDmScope = await db.CampaignCharacters.AsNoTracking()
            .AnyAsync(association =>
                association.CampaignId == campaignId &&
                association.CharacterId == characterId &&
                association.Status == CampaignCharacterAssociationStatus.Active &&
                association.Campaign.Status == CampaignStatus.Active &&
                association.Character.Status == CharacterStatus.Active &&
                association.Character.ArchivedAt == null &&
                db.CampaignMemberships.Any(membership =>
                    membership.CampaignId == campaignId &&
                    membership.UserId == authorizedActor &&
                    membership.Status == CampaignMembershipStatus.Active &&
                    membership.Roles.Any(role => role.Role == CampaignRoles.Dm)),
                cancellationToken);
        if (!hasActiveDmScope)
            return NotFound();

        return Ok(new
        {
            contractVersion = 1,
            sourceToolKey = sourceKey,
            actorUserId = authorizedActor,
            campaignId,
            characterId,
            hasActiveDmScope = true
        });
    }

    private async Task<Guid?> AuthorizedBackendActorAsync(
        string sourceKey, CancellationToken cancellationToken)
    {
        if (sourceKey != CharacterToolContract.Slug)
            return null;

        // Only the server-issued, registration-scoped delegation capability
        // is accepted. Browser cookies, user ID headers and ordinary tickets
        // are not evidence for live owner or DM authority.
        var header = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase) ||
            header.Length <= bearer.Length ||
            !capabilities.TryUse(sourceKey, header[bearer.Length..].Trim(), out var context) ||
            context is null ||
            context.RegistrationKey != sourceKey ||
            context.DelegatedFromToolKey is not null ||
            context.PrivateTunnelSourceToolKey is not null ||
            context.SiteMode != SiteModeValues.DorksAndDiceModeValue ||
            !Guid.TryParse(context.User.Id, out var actor) || actor == Guid.Empty)
            return null;

        var registration = await tools.GetByKeyAsync(sourceKey, cancellationToken);
        if (registration is null || !registration.Enabled ||
            registration.Kind != ToolKind.Application ||
            registration.Slug != CharacterToolContract.Slug ||
            !ToolVisibility.IsVisibleInMode(registration, context.SiteMode) ||
            !ToolApplicationAccessPolicy.CanAccessFromHostContext(registration, context))
            return null;

        return actor;
    }

}
