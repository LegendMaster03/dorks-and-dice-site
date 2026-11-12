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

    Task<ToolHostAuthenticationContext?> CreateDelegatedAsync(
        ToolRegistration targetTool,
        ToolHostAuthenticationContext sourceContext,
        CancellationToken cancellationToken = default);

    Task<ToolHostAuthenticationContext?> CreatePrivateTunnelAsync(
        ToolRegistration targetTool,
        ToolHostAuthenticationContext sourceContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates registration-scoped Tool authentication contexts and delegated
/// contexts. Campaign and character projections currently depend directly
/// on Dorks & Dice mode services; extraction must address that integration
/// rather than claiming the factory is already mode-agnostic.
/// </summary>
public sealed class ToolHostAuthenticationContextFactory(
    ICampaignContextService campaignContextService,
    ICharacterService characterService,
    IToolRegistry toolRegistry) : IToolHostAuthenticationContextFactory
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
            tool.Key,
            userId,
            cancellationToken);

        var dmScopes = await BuildDmCharacterScopesAsync(
            tool.Key, userId, campaigns, cancellationToken);

        return new ToolHostAuthenticationContext
        {
            ToolKey = tool.Key,
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
            ScopedRoles = ScopedAccountRoles.ForScope(siteMode)
                .Where(role => AccountRoleHierarchy.PrincipalHasScopedRole(principal, siteMode, role))
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
            Characters = characters,
            DmCharacterScopes = dmScopes
        };
    }

    public Task<ToolHostAuthenticationContext?> CreateDelegatedAsync(
        ToolRegistration targetTool,
        ToolHostAuthenticationContext sourceContext,
        CancellationToken cancellationToken = default) =>
        CreateTargetScopedAsync(
            targetTool,
            sourceContext,
            ordinaryDelegation: true,
            cancellationToken);

    public Task<ToolHostAuthenticationContext?> CreatePrivateTunnelAsync(
        ToolRegistration targetTool,
        ToolHostAuthenticationContext sourceContext,
        CancellationToken cancellationToken = default) =>
        CreateTargetScopedAsync(
            targetTool,
            sourceContext,
            ordinaryDelegation: false,
            cancellationToken);

    private async Task<ToolHostAuthenticationContext?> CreateTargetScopedAsync(
        ToolRegistration targetTool,
        ToolHostAuthenticationContext sourceContext,
        bool ordinaryDelegation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetTool);
        ArgumentNullException.ThrowIfNull(sourceContext);

        // A capability may remain valid briefly after its source application's release audience
        // changes. Re-read the current source registration and re-apply application audience
        // authorization before creating any delegated/private-tunnel target context. Structural
        // source availability remains the calling controller's responsibility, and services keep
        // their separate capability policy.
        var sourceTool = await toolRegistry.GetByKeyAsync(
            sourceContext.RegistrationKey,
            cancellationToken);
        if (sourceTool?.Kind == ToolKind.Application
            && !ToolApplicationAccessPolicy.CanAccessFromHostContext(sourceTool, sourceContext))
        {
            return null;
        }

        // Services are governed by the separate delegation/private-tunnel service policy. When
        // the target is an application, the initiating user's authoritative Tool Host context
        // must independently satisfy that destination application's current mode and release
        // audience so an accessible source Tool can not act as a confused deputy.
        if (targetTool.Kind == ToolKind.Application
            && !ToolApplicationAccessPolicy.CanAccessFromHostContext(targetTool, sourceContext))
        {
            return null;
        }

        if (!Guid.TryParse(sourceContext.User.Id, out var userId))
        {
            return null;
        }

        var characters = await BuildCharacterAccessProjectionAsync(
            targetTool.Key,
            userId,
            cancellationToken);

        // The source capability is short-lived, but DM target scope must still
        // be recomputed from the Site's current campaign data for this target.
        var currentCampaigns = await campaignContextService.GetAccessibleCampaignsAsync(
            userId, cancellationToken);
        var dmScopes = await BuildDmCharacterScopesAsync(
            targetTool.Key, userId, currentCampaigns, cancellationToken);

        return new ToolHostAuthenticationContext
        {
            ToolKey = targetTool.Key,
            ToolSlug = targetTool.Slug,
            DelegatedFromToolKey = ordinaryDelegation ? sourceContext.RegistrationKey : null,
            DelegatedFromToolSlug = ordinaryDelegation ? sourceContext.ToolSlug : null,
            PrivateTunnelSourceToolKey = ordinaryDelegation ? null : sourceContext.RegistrationKey,
            PrivateTunnelSourceToolSlug = ordinaryDelegation ? null : sourceContext.ToolSlug,
            SiteMode = sourceContext.SiteMode,
            User = new ToolHostUserContext
            {
                Id = sourceContext.User.Id,
                DisplayName = sourceContext.User.DisplayName
            },
            GlobalRoles = sourceContext.GlobalRoles.ToArray(),
            ScopedRoles = sourceContext.ScopedRoles.ToArray(),
            Campaigns = sourceContext.Campaigns
                .Select(campaign => new ToolHostCampaignAccessSummary
                {
                    Id = campaign.Id,
                    Name = campaign.Name,
                    Role = campaign.Role
                })
                .ToArray(),
            Characters = characters,
            DmCharacterScopes = dmScopes
        };
    }

    /// <summary>
    /// Computes DM-target associations from live Site campaign data. A DM role
    /// alone is insufficient: the character must also be actively linked to
    /// that campaign. This is authority to submit an approval for review, not
    /// ownership or permission to read private sheet content.
    /// </summary>
    private async Task<IReadOnlyList<ToolHostDmCharacterScope>?> BuildDmCharacterScopesAsync(
        string toolKey, Guid userId, IReadOnlyList<CampaignAccessContext> campaigns,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(toolKey, CharacterToolContract.Slug, StringComparison.Ordinal))
            return null;

        var targets = new List<ToolHostDmCharacterScope>();
        foreach (var campaign in campaigns.Where(campaign =>
            campaign.Roles.Contains(CampaignRoles.Dm, StringComparer.OrdinalIgnoreCase)))
        {
            var context = await campaignContextService.GetCampaignContextAsync(
                userId, campaign.CampaignId, cancellationToken);
            if (context is null ||
                !context.RequestingUserRoles.Contains(CampaignRoles.Dm, StringComparer.OrdinalIgnoreCase))
                continue;

            targets.AddRange(context.Characters
                .Where(character => character.CharacterId != Guid.Empty)
                .Select(character => new ToolHostDmCharacterScope
                {
                    CampaignId = context.CampaignId,
                    CharacterId = character.CharacterId
                }));
        }
        return targets
            .DistinctBy(target => (target.CampaignId, target.CharacterId))
            .OrderBy(target => target.CampaignId)
            .ThenBy(target => target.CharacterId)
            .ToArray();
    }

    private async Task<IReadOnlyList<ToolHostCharacterAccessSummary>?> BuildCharacterAccessProjectionAsync(
        string toolKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(toolKey, CharacterToolContract.Slug, StringComparison.Ordinal))
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
