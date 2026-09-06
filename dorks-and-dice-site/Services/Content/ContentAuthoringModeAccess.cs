using System.Security.Claims;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Centralizes mode-aware content authoring rules. Normal editor requests are bound to one normal
/// mode. Synthetic Development is a global editor context and can span normal mode assignments,
/// while still relying on the caller's Global Editor authority.
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

        if (modeContext.SyntheticMode is not null)
        {
            return modeContext.HasTrustedAccess
                && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor)
                && item.VisibleInModes.All(modeId => CanEditMode(principal, modeId));
        }

        return CanEditItem(principal, item, RequireActiveModeId(modeContext));
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

        // Shared content affects every assigned mode. A normal mode editor may edit it only when
        // the principal has Editor authority for every affected mode. Assignment itself remains
        // immutable on the normal editor surface.
        return item.VisibleInModes.All(modeId => CanEditMode(principal, modeId));
    }

    public static bool CanSelectModes(ClaimsPrincipal principal, SiteModeContext modeContext) =>
        modeContext.SyntheticMode is not null
        && modeContext.HasTrustedAccess
        && AccountRoleHierarchy.PrincipalHasGlobalRole(principal, AccountRoles.GlobalEditor);

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
}
