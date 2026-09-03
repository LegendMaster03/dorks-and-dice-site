using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/databases")]
public sealed class ContentDatabaseManagementController : Controller
{
    private readonly IContentAuthoringService _authoringService;

    public ContentDatabaseManagementController(IContentAuthoringService authoringService)
    {
        _authoringService = authoringService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? source, CancellationToken cancellationToken)
    {
        return View(await _authoringService.GetIndexAsync(source, cancellationToken));
    }

    [HttpPost("{slug}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(
        string slug,
        string source,
        string targetSource,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authoringService.MoveAsync(source, targetSource, slug, cancellationToken);
            TempData["ContentDatabaseSuccess"] = $"Moved '{slug}' from {source} to {targetSource}.";
            return RedirectToAction(nameof(Index), new { source = targetSource });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentDatabaseError"] = ex.Message;
            return RedirectToAction(nameof(Index), new { source });
        }
    }

    [HttpPost("push-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushAll(
        string source,
        string targetSource,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await _authoringService.MoveAllAsync(source, targetSource, cancellationToken);
            TempData["ContentDatabaseSuccess"] = $"Moved {count} content page(s) from {source} to {targetSource}.";
            return RedirectToAction(nameof(Index), new { source = targetSource });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentDatabaseError"] = ex.Message;
            return RedirectToAction(nameof(Index), new { source });
        }
    }
}
