using dorks_and_dice_site.Plugins.Discord;
using dorks_and_dice_site.Plugins.DiscordBot;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Discord;

public sealed class DorksAndDiceDiscordLinkedAccountProjectionSource(
    IdentityDbContext identityDbContext,
    IModeExternalConnectionRegistry modeConnections) : IDiscordWorkspaceProjectionSource
{
    public const string ProjectionSourceId = "dorks-and-dice-linked-accounts";
    private const string ModeId = SiteModeValues.DorksAndDiceModeValue;

    public string SourceId => ProjectionSourceId;

    public async Task<IReadOnlyCollection<DiscordGuildWorkspaceProjection>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        if (!modeConnections.TryGet(
                ModeId,
                DiscordProvider.Id,
                out var modeConnection)
            || modeConnection is null)
        {
            return [];
        }

        var userIds = await (
            from activation in identityDbContext.AccountLinkModeActivations
            join login in identityDbContext.UserLogins
                on activation.UserId equals login.UserId
            where activation.ModeId == ModeId
                && activation.ProviderId == DiscordProvider.Id
                && activation.ResourceId == modeConnection.ResourceId
                && login.LoginProvider == DiscordProvider.Id
            select activation.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return
        [
            new DiscordGuildWorkspaceProjection(
                ModeId,
                modeConnection.ResourceId,
                modeConnection.ResourceId,
                [
                    new DiscordDesiredRole(
                        "linked-account",
                        "Linked Account",
                        userIds)
                ],
                [])
        ];
    }
}
