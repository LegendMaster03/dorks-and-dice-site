using System.Security.Claims;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Services.Tools;

public interface IToolHostAuthenticationContextFactory
{
    Task<ToolHostAuthenticationContext?> CreateAsync(
        ToolRegistration tool,
        ClaimsPrincipal principal,
        string siteMode,
        CancellationToken cancellationToken = default);
}

public sealed class ToolHostAuthenticationContextFactory(
    ICampaignContextService campaignContextService,
    ICharacterService characterService) : IToolHostAuthenticationContextFactory
{
    public async Task<ToolHostAuthenticationContext?> CreateAsync(
        ToolRegistration tool,
        ClaimsPrincipal principal,
        string siteMode,
        CancellationToken cancellationToken = default)
    {
        var userIdText = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId))
        {
            return null;
        }

        var campaigns = await campaignContextService.GetAccessibleCampaignsAsync(
            userId,
            cancellationToken);
        var characters = await BuildCharacterAccessProjectionAsync(
            tool.Slug,
            userId,
            cancellationToken);

        return new ToolHostAuthenticationContext
        {
            ToolSlug = tool.Slug,
            SiteMode = siteMode,
            User = new ToolHostUserContext
            {
                Id = userIdText!,
                DisplayName = principal.FindFirstValue(AccountClaimTypes.DisplayName)
                    ?? principal.Identity?.Name
                    ?? string.Empty
            },
            GlobalRoles = AccountRoleHierarchy.GlobalRoleNames
                .Where(role => AccountRoleHierarchy.PrincipalHasGlobalRole(principal, role))
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray(),
            Campaigns = campaigns
                .SelectMany(campaign => campaign.Roles.Select(role => new ToolHostCampaignAccessSummary
                {
                    Id = campaign.CampaignId,
                    Name = campaign.Name,
                    Role = string.Equals(role, CampaignRoles.Dm, StringComparison.OrdinalIgnoreCase)
                        ? "DM"
                        : "Player"
                }))
                .ToArray(),
            Characters = characters
        };
    }

    private async Task<IReadOnlyList<ToolHostCharacterAccessSummary>?> BuildCharacterAccessProjectionAsync(
        string toolSlug,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(toolSlug, CharacterToolContract.Slug, StringComparison.Ordinal))
        {
            return null;
        }

        var characters = await characterService.GetForOwnerIncludingArchivedAsync(
            userId,
            cancellationToken);
        return characters.Select(character => new ToolHostCharacterAccessSummary
        {
            Id = character.Id,
            Name = character.Name,
            Status = character.Status.ToString(),
            ArchivedAt = character.ArchivedAt,
            CampaignIds = character.CampaignAssociations
                .Where(association => association.Status == CampaignCharacterAssociationStatus.Active)
                .Select(association => association.CampaignId)
                .Distinct()
                .OrderBy(campaignId => campaignId)
                .ToArray()
        }).ToArray();
    }
}
