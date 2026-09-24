using System.Security.Claims;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Modes.DorksAndDice.Ui;

[Authorize]
[Route("discord-servers")]
public sealed class DiscordServersController(
    IDorksAndDiceDiscordServerService discordServers,
    IDiscordBotInstallLinkProvider installLinkProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return View(await BuildAsync(userId, cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(
        Guid? bindingId,
        string guildId,
        DiscordServerCampaignScope campaignScope,
        Guid[]? campaignIds,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var binding = await discordServers.ConfigureAsync(
                userId,
                bindingId,
                guildId,
                campaignScope,
                campaignIds ?? [],
                cancellationToken);
            TempData["DiscordServerMessage"] =
                $"Discord server '{binding.GuildName}' configured.";
        }
        catch (CampaignDomainException exception)
        {
            TempData["DiscordServerError"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{bindingId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        Guid bindingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await discordServers.RemoveAsync(
            userId,
            bindingId,
            cancellationToken);
        TempData["DiscordServerMessage"] =
            "Discord server removed. Bot-managed roles and channels will be cleaned up on the next synchronization pass.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<DiscordServersViewModel> BuildAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var eligibleCampaigns = await discordServers.GetEligibleCampaignsAsync(
            userId,
            cancellationToken);
        var bindings = await discordServers.GetForOwnerAsync(
            userId,
            cancellationToken);

        return new DiscordServersViewModel
        {
            InstallUrl = installLinkProvider.CreateInstallUrl(),
            EligibleCampaigns = eligibleCampaigns
                .Select(campaign => new DiscordServerCampaignOptionViewModel(
                    campaign.Id,
                    campaign.Name))
                .ToArray(),
            Servers = bindings
                .Select(binding => new DiscordServerBindingViewModel(
                    binding.Id,
                    binding.GuildId,
                    binding.GuildName,
                    binding.CampaignScope,
                    binding.Campaigns
                        .OrderBy(selection => selection.Campaign.Name)
                        .Select(selection => new DiscordServerCampaignOptionViewModel(
                            selection.CampaignId,
                            selection.Campaign.Name))
                        .ToArray()))
                .ToArray()
        };
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
            out userId);
}
