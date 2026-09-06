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
        if (!ModeScopedRoleAccess.PrincipalHasRoleForContext(
                User,
                modeContext,
                ScopedAccountRoles.Editor))
        {
            return Forbid();
        }

        // The editor landing page no longer chooses a mode. Normal hosted requests already have
        // a mode and synthetic Development intentionally spans normal modes, so the compatibility
        // route can go directly to the content workspace.
        return Redirect("/editor/content");
    }
}
