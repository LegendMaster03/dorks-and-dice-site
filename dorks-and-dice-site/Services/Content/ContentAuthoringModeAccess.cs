using System.Security.Claims;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Centralizes mode-aware content authoring rules. A selected normal mode always scopes the
/// editor to that mode, including when the request is hosted inside synthetic Development.
/// Synthetic Development becomes a global authoring context only when Development itself is the
/// selected ribbon mode and the caller has both developer preview capability and Global Editor
/// authority.
/// </summary>
public static class ContentAuthoringModeAccess
{
    public static string RequireActiveModeId(SiteModeContext modeContext)
    {
        ArgumentNullException.ThrowIfNull(modeContext);
        return modeContext.ActiveModeId
            ?? throw new InvalidOperationException("Content authoring requires an active normal site mode.");
    }

    public static bool CanEditMode(ClaimsPrincipal principal, string modeId) =>
        AccountRoleHierarchy.PrincipalHasScopedRole(principal, modeId, ScopedAccountRoles.Editor);

    public static bool CanEditItem(
        ClaimsPrincipal principal,
        ContentItem item,
        SiteModeContext modeContext)
    {
        ArgumentNullException.ThrowIfNull(modeContext);

        if (CanUseGlobalDevelopmentAuthoring(principal, modeContext))
        {
            return item.VisibleInModes.All(modeId => CanEditMode(principal, modeId));
        }

        if (modeContext.ActiveModeId is { Length: > 0 } activeModeId)
        {
            return CanEditItem(principal, item, activeModeId);
        }

        return false;
    }

    public static bool CanEditItem(
        ClaimsPrincipal principal,
        ContentItem item,
        string activeModeId)
    {
        if (!item.VisibleInModes.Contains(activeModeId, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Shared content affects every assigned mode. A normal/scoped editor may edit it only when
        // the principal has Editor authority for every affected mode. Assignment itself remains
        // immutable on the scoped editor surface.
        return item.VisibleInModes.All(modeId => CanEditMode(principal, modeId));
    }

    public static bool CanSelectModes(ClaimsPrincipal principal, SiteModeContext modeContext) =>
        CanUseGlobalDevelopmentAuthoring(principal, modeContext);

    public static void ForceNewDocumentMode(ContentAuthoringDocument document, string activeModeId)
    {
        document.VisibleModesSelection = [activeModeId];
        document.VisibleModesText = activeModeId;
    }

    public static void PreserveExistingDocumentModes(
        ContentAuthoringDocument submitted,
        ContentAuthoringDocument current)
    {
        submitted.VisibleModesSelection = current.VisibleModesSelection.ToList();
        submitted.VisibleModesText = current.VisibleModesText;
    }

    private static bool CanUseGlobalDevelopmentAuthoring(
        ClaimsPrincipal principal,
        SiteModeContext modeContext) =>
        modeContext.SyntheticMode is not null
        && modeContext.ActiveMode is null
        && modeContext.HasTrustedAccess
        && modeContext.IsDevelopmentPreview
        && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor);
}
