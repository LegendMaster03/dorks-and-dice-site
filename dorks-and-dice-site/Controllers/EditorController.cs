using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
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
        var isAdmin = User.IsInRole(AccountRoles.Admin);
        var canEditDorksAndDice = isAdmin || HasScopedEditorRole(AccountRoleScopes.DorksAndDice);
        var canEditProfessional = isAdmin || HasScopedEditorRole(AccountRoleScopes.Professional);

        if (!canEditDorksAndDice && !canEditProfessional)
        {
            return Forbid();
        }

        ViewData["CanEditDorksAndDice"] = canEditDorksAndDice;
        ViewData["CanEditProfessional"] = canEditProfessional;
        return View();
    }

    private bool HasScopedEditorRole(string scope)
    {
        var expectedValue = $"{scope}:{ScopedAccountRoles.Editor}";
        return User.HasClaim(AccountClaimTypes.ScopedRole, expectedValue);
    }
}
