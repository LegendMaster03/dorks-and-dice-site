using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Shared administrative landing page, guarded by the global admin policy.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.AdminAccess)]
[Route("admin")]
public sealed class AdminController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
