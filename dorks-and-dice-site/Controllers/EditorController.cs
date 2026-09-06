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

        // Trusted developers use the central authoring surface. This keeps /editor useful as a
        // stable entry point while avoiding the now-redundant editor-selection page.
        if (modeContext.HasTrustedAccess
            && AccountRoleHierarchy.PrincipalHasGlobalRole(User, AccountRoles.Dev))
        {
            return Redirect("/development/content");
        }

        var activeMode = modeContext.ActiveMode;
        if (activeMode is not null
            && AccountRoleHierarchy.PrincipalHasScopedRole(
                User,
                activeMode.Id,
                ScopedAccountRoles.Editor))
        {
            return Redirect("/editor/content");
        }

        return Forbid();
    }
}
