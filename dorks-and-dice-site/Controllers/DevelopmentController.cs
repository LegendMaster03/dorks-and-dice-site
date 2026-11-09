using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

/// <summary>
/// Landing page for the synthetic Development control plane, not a third site.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development")]
public sealed class DevelopmentController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
