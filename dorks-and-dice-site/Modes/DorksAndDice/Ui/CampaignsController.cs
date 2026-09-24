using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

[Authorize]
[Route("campaigns")]
public sealed class CampaignsController(ICampaignService campaignService, ICampaignParticipantService participantService, ICampaignInvitationService invitationService) : Controller
{
    private readonly ICampaignService _campaignService = campaignService;
    private readonly ICampaignParticipantService _participantService = participantService;
    private readonly ICampaignInvitationService _invitationService = invitationService;

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
        try
        {
            var campaign = await _campaignService.CreateAsync(userId, name, cancellationToken);
            return RedirectToAction(nameof(Details), new { campaignId = campaign.Id });
        }
        catch (CampaignDomainException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(nameof(Index), await BuildIndexAsync(userId, cancellationToken));
        }
    }

    [HttpGet("{campaignId:guid}")]
    public async Task<IActionResult> Details(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var model = await BuildDetailsAsync(userId, campaignId, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("{campaignId:guid}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid campaignId, string name, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RenameAsync(userId, campaignId, name, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.ArchiveAsync(userId, campaignId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RestoreAsync(userId, campaignId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/leave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _campaignService.LeaveAsync(userId, campaignId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (CampaignDomainException exception)
        {
            TempData["CampaignError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { campaignId });
        }
    }

    [HttpPost("{campaignId:guid}/members/{memberUserId:guid}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMemberRoles(Guid campaignId, Guid memberUserId, bool player, bool dm, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var roles = new List<string>();
        if (player) roles.Add(CampaignRoles.Player);
        if (dm) roles.Add(CampaignRoles.Dm);
        try { await _campaignService.SetMemberRolesAsync(userId, campaignId, memberUserId, roles, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/members/{memberUserId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(Guid campaignId, Guid memberUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _campaignService.RemoveMemberAsync(userId, campaignId, memberUserId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/participants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParticipant(Guid campaignId, string displayName, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.AddGuestAsync(userId, campaignId, displayName, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/participants/{participantId:guid}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameParticipant(Guid campaignId, Guid participantId, string displayName, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RenameAsync(userId, campaignId, participantId, displayName, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/participants/{participantId:guid}/retire")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetireParticipant(Guid campaignId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RetireAsync(userId, campaignId, participantId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/participants/{participantId:guid}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreParticipant(Guid campaignId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _participantService.RestoreAsync(userId, campaignId, participantId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/invitations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvitation(Guid campaignId, bool player, bool dm, Guid? participantId, string? displayName, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var roles = new List<string>();
        if (player) roles.Add(CampaignRoles.Player);
        if (dm) roles.Add(CampaignRoles.Dm);
        try
        {
            if (roles.Count == 0)
            {
                throw new CampaignDomainException("Select at least one campaign role for the invitation.");
            }

            var resolvedParticipantId = participantId;
            if (resolvedParticipantId is null)
            {
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    throw new CampaignDomainException("Enter the player's name or select an existing participant.");
                }

                var participant = await _participantService.AddGuestAsync(userId, campaignId, displayName, cancellationToken);
                resolvedParticipantId = participant.Id;
            }

            var grant = await _invitationService.CreateAsync(userId, campaignId, roles, resolvedParticipantId, cancellationToken);
            TempData["CampaignInviteLink"] = Url.Action(nameof(Invitation), "Campaigns", new { token = grant.Token }, Request.Scheme) ?? $"/campaigns/invitations/{grant.Token}";
        }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpPost("{campaignId:guid}/invitations/{invitationId:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvitation(Guid campaignId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _invitationService.RevokeAsync(userId, campaignId, invitationId, cancellationToken); }
        catch (CampaignDomainException exception) { TempData["CampaignError"] = exception.Message; }
        return RedirectToAction(nameof(Details), new { campaignId });
    }

    [HttpGet("invitations/{token}")]
    public async Task<IActionResult> Invitation(string token, CancellationToken cancellationToken)
    {
        var preview = await _invitationService.GetPreviewAsync(token, cancellationToken);
        return preview is null ? NotFound() : View(ToInvitationPageModel(preview, token));
    }

    [HttpPost("invitations/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvitation(string token, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var preview = await _invitationService.GetPreviewAsync(token, cancellationToken);
        if (preview is null) return NotFound();
        try
        {
            var membership = await _invitationService.AcceptAsync(userId, token, cancellationToken);
            return RedirectToAction(nameof(Details), new { campaignId = membership.CampaignId });
        }
        catch (CampaignDomainException exception)
        {
            return View(nameof(Invitation), ToInvitationPageModel(preview, token, exception.Message));
        }
    }

    private async Task<CampaignsIndexViewModel> BuildIndexAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _campaignService.GetForUserAsync(userId, cancellationToken);
        var archived = await _campaignService.GetArchivedForUserAsync(userId, cancellationToken);
        return new CampaignsIndexViewModel
        {
            Campaigns = active.Select(campaign => ToListItem(campaign, userId)).ToArray(),
            ArchivedCampaigns = archived.Select(campaign => ToListItem(campaign, userId)).ToArray()
        };
    }

    private static CampaignListItemViewModel ToListItem(Campaign campaign, Guid userId)
    {
        var membership = campaign.Memberships.Single(item => item.UserId == userId);
        return new CampaignListItemViewModel(campaign.Id, campaign.Name, membership.Roles.Select(role => role.Role).OrderBy(role => role).ToArray());
    }

    private async Task<CampaignDetailsViewModel?> BuildDetailsAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _campaignService.GetAsync(userId, campaignId, cancellationToken);
        if (campaign is null) return null;
        var activeMemberships = campaign.Memberships.Where(membership => membership.Status == CampaignMembershipStatus.Active).ToArray();
        var currentMembership = activeMemberships.Single(membership => membership.UserId == userId);
        var currentRoles = currentMembership.Roles.Select(role => role.Role).OrderBy(role => role).ToArray();
        var canManage = currentRoles.Contains(CampaignRoles.Dm, StringComparer.Ordinal);
        var activeParticipants = campaign.Participants.Where(participant => participant.Status == CampaignParticipantStatus.Active).ToArray();
        IReadOnlyList<CampaignInvitation> invitations = canManage && campaign.Status == CampaignStatus.Active
            ? await _invitationService.GetPendingAsync(userId, campaignId, cancellationToken) : [];

        return new CampaignDetailsViewModel
        {
            Id = campaign.Id,
            Name = campaign.Name,
            IsArchived = campaign.Status == CampaignStatus.Archived,
            ArchivedAt = campaign.ArchivedAt,
            CurrentUserId = userId,
            CanManage = canManage,
            CurrentUserRoles = currentRoles,
            Members = activeMemberships.OrderBy(membership => membership.JoinedAt).Select(membership => new CampaignMemberViewModel(
                membership.UserId,
                activeParticipants.FirstOrDefault(participant => participant.UserId == membership.UserId)?.DisplayName,
                membership.Roles.Select(role => role.Role).OrderBy(role => role).ToArray(),
                membership.UserId == userId)).ToArray(),
            Participants = campaign.Participants.OrderBy(participant => participant.Status).ThenBy(participant => participant.DisplayName)
                .Select(participant => new CampaignParticipantViewModel(participant.Id, participant.DisplayName, participant.UserId, participant.Status == CampaignParticipantStatus.Active)).ToArray(),
            Invitations = invitations.Select(invitation => new CampaignInvitationListItemViewModel(
                invitation.Id,
                invitation.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                invitation.Participant?.DisplayName,
                invitation.ExpiresAt)).ToArray(),
            InviteableParticipants = campaign.Participants
                .Where(participant => (participant.Status == CampaignParticipantStatus.Active && participant.UserId is null) || participant.Status == CampaignParticipantStatus.Former)
                .OrderBy(participant => participant.DisplayName)
                .Select(participant => new ParticipantOptionViewModel(participant.Id, participant.DisplayName, participant.Status == CampaignParticipantStatus.Former)).ToArray()
        };
    }

    private static CampaignInvitationPageViewModel ToInvitationPageModel(CampaignInvitationPreview preview, string token, string? error = null) =>
        new() { Token = token, CampaignId = preview.CampaignId, CampaignName = preview.CampaignName, Roles = preview.Roles, ParticipantName = preview.ParticipantName, ExpiresAt = preview.ExpiresAt, Error = error };

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
