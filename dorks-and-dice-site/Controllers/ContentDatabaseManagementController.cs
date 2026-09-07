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
            var sourceIndex = await _authoringService.GetIndexAsync(source, cancellationToken);
            var sourceItem = sourceIndex.Items.SingleOrDefault(item =>
                string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Content page '{slug}' no longer exists in {source}.");

            var targetIndex = await _authoringService.GetIndexAsync(targetSource, cancellationToken);
            if (targetIndex.Items.Any(item =>
                    string.Equals(item.Id, sourceItem.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Slug, sourceItem.Slug, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"The target source already contains a page using stable ID '{sourceItem.Id}' or slug '{sourceItem.Slug}'. Use a deliberate reconciliation workflow instead of Move.");
            }

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
}
