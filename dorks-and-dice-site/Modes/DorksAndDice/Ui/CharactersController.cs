using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

[Authorize]
[Route("characters")]
public sealed class CharactersController(ICharacterService characterService, ICampaignService campaignService) : Controller
{
    private readonly ICharacterService _characterService = characterService;
    private readonly ICampaignService _campaignService = campaignService;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return View(await BuildIndexAsync(userId, cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.CreateAsync(userId, name, cancellationToken); }
        catch (CampaignDomainException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(nameof(Index), await BuildIndexAsync(userId, cancellationToken));
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{characterId:guid}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid characterId, string name, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.RenameAsync(userId, characterId, name, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CharacterError"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{characterId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid characterId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.ArchiveAsync(userId, characterId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CharacterError"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{characterId:guid}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid characterId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.RestoreAsync(userId, characterId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CharacterError"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{characterId:guid}/connect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(Guid characterId, Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.ConnectToCampaignAsync(userId, characterId, campaignId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CharacterError"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{characterId:guid}/disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(Guid characterId, Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _characterService.DisconnectFromCampaignAsync(userId, characterId, campaignId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CharacterError"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<CharactersIndexViewModel> BuildIndexAsync(Guid userId, CancellationToken cancellationToken)
    {
        var characters = await _characterService.GetForOwnerIncludingArchivedAsync(userId, cancellationToken);
        var campaigns = await _campaignService.GetForUserAsync(userId, cancellationToken);
        var playerCampaigns = campaigns.Where(campaign => campaign.Memberships.Any(membership =>
                membership.UserId == userId && membership.Status == CampaignMembershipStatus.Active
                && membership.Roles.Any(role => role.Role == CampaignRoles.Player)))
            .OrderBy(campaign => campaign.Name).Select(campaign => new CampaignOptionViewModel(campaign.Id, campaign.Name)).ToArray();

        CharacterListItemViewModel Map(Character character) => new(
            character.Id,
            character.Name,
            character.ArchivedAt,
            character.CampaignAssociations.Where(association => association.Status == CampaignCharacterAssociationStatus.Active)
                .OrderBy(association => association.Campaign.Name)
                .Select(association => new CharacterCampaignConnectionViewModel(association.CampaignId, association.Campaign.Name)).ToArray());

        return new CharactersIndexViewModel
        {
            ActiveCharacters = characters.Where(character => character.Status == CharacterStatus.Active).Select(Map).ToArray(),
            ArchivedCharacters = characters.Where(character => character.Status == CharacterStatus.Archived).Select(Map).ToArray(),
            PlayerCampaigns = playerCampaigns
        };
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
