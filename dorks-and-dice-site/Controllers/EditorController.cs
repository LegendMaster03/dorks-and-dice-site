using dorks_and_dice_site.Models.Editor;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize]
[Route("editor")]
public sealed class EditorController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var activeMode = modeContext.ActiveMode;
        if (activeMode is null)
        {
            if (modeContext.IsTrustedPreview
                && modeContext.HasTrustedAccess
                && AccountRoleHierarchy.PrincipalHasGlobalRole(User, AccountRoles.GlobalEditor))
            {
                return Redirect("/editor/content");
            }

            return Forbid();
        }

        if (!AccountRoleHierarchy.PrincipalHasScopedRole(
                User,
                activeMode.Id,
                ScopedAccountRoles.Editor))
        {
            return Forbid();
        }

        return View(new EditorIndexViewModel
        {
            IsTrustedPreview = modeContext.HasTrustedAccess,
            Modes =
            [
                new EditorModeOption
                {
                    ModeId = activeMode.Id,
                    DisplayName = activeMode.DisplayName,
                    EditorHref = "/editor/content"
                }
            ]
        });
    }
}
