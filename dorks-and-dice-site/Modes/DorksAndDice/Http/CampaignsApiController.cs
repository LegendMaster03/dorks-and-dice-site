using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Http;

[ApiController]
[Authorize]
[Route("campaigns/api")]
public sealed class CampaignsApiController(ICampaignService campaignService, ICampaignParticipantService participantService) : ControllerBase
{
    private readonly ICampaignService _campaignService = campaignService;
    private readonly ICampaignParticipantService _participantService = participantService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignSummaryResponse>>> GetCampaigns([FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var active = await _campaignService.GetForUserAsync(userId, cancellationToken);
        var campaigns = active.ToList();
        if (includeArchived) campaigns.AddRange(await _campaignService.GetArchivedForUserAsync(userId, cancellationToken));
        return Ok(campaigns.Select(campaign => DorksAndDiceApiMapper.ToSummary(campaign, userId)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CampaignSummaryResponse>> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var campaign = await _campaignService.CreateAsync(userId, request.Name, cancellationToken);
            return Created($"/campaigns/api/{campaign.Id}", DorksAndDiceApiMapper.ToSummary(campaign, userId));
        }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet("{campaignId:guid}")]
    public async Task<ActionResult<CampaignDetailsResponse>> GetCampaign(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var campaign = await _campaignService.GetAsync(userId, campaignId, cancellationToken);
        return campaign is null ? NotFound() : Ok(DorksAndDiceApiMapper.ToDetails(campaign, userId));
    }

    [HttpPut("{campaignId:guid}/name")]
    public async Task<IActionResult> RenameCampaign(Guid campaignId, [FromBody] RenameCampaignRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RenameAsync(userId, campaignId, request.Name, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/archive")]
    public async Task<IActionResult> ArchiveCampaign(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.ArchiveAsync(userId, campaignId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/restore")]
    public async Task<IActionResult> RestoreCampaign(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RestoreAsync(userId, campaignId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid campaignId, [FromBody] AddCampaignMemberRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.AddMemberAsync(userId, campaignId, request.UserId, request.Roles, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{campaignId:guid}/members/{memberUserId:guid}/roles")]
    public async Task<IActionResult> SetMemberRoles(Guid campaignId, Guid memberUserId, [FromBody] SetCampaignMemberRolesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.SetMemberRolesAsync(userId, campaignId, memberUserId, request.Roles, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/leave")]
    public async Task<IActionResult> LeaveCampaign(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.LeaveAsync(userId, campaignId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpDelete("{campaignId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid campaignId, Guid memberUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RemoveMemberAsync(userId, campaignId, memberUserId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet("{campaignId:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<CampaignParticipantResponse>>> GetParticipants(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var participants = await _participantService.GetForCampaignAsync(userId, campaignId, cancellationToken);
            return Ok(participants.Select(DorksAndDiceApiMapper.ToParticipant).ToArray());
        }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/participants")]
    public async Task<ActionResult<CampaignParticipantResponse>> AddParticipant(Guid campaignId, [FromBody] AddCampaignParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var participant = await _participantService.AddGuestAsync(userId, campaignId, request.DisplayName, cancellationToken);
            return Created($"/campaigns/api/{campaignId}/participants/{participant.Id}", DorksAndDiceApiMapper.ToParticipant(participant));
        }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{campaignId:guid}/participants/{participantId:guid}/name")]
    public async Task<IActionResult> RenameParticipant(Guid campaignId, Guid participantId, [FromBody] RenameCampaignParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RenameAsync(userId, campaignId, participantId, request.DisplayName, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/participants/{participantId:guid}/link")]
    public async Task<IActionResult> LinkParticipant(Guid campaignId, Guid participantId, [FromBody] LinkCampaignParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.LinkToUserAsync(userId, campaignId, participantId, request.UserId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{campaignId:guid}/participants/{participantId:guid}/restore")]
    public async Task<IActionResult> RestoreParticipant(Guid campaignId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RestoreAsync(userId, campaignId, participantId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpDelete("{campaignId:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RetireParticipant(Guid campaignId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RetireAsync(userId, campaignId, participantId, cancellationToken); return NoContent(); }
        catch (CampaignDomainException exception) { return BadRequest(new { error = exception.Message }); }
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
