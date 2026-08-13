using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("development/content")]
[RequestSizeLimit(ContentInputPolicy.MaxAuthoringRequestBytes)]
public sealed class ContentAuthoringController : Controller
{
    private readonly IContentAuthoringService _authoringService;
    private readonly IContentSourceTransferService _transferService;
    private readonly IContentBodyRenderer _bodyRenderer;

    public ContentAuthoringController(
        IContentAuthoringService authoringService,
        IContentSourceTransferService transferService,
        IContentBodyRenderer bodyRenderer)
    {
        _authoringService = authoringService;
        _transferService = transferService;
        _bodyRenderer = bodyRenderer;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        var sourceKey = Request.Query["source"].FirstOrDefault();
        return View(await _authoringService.GetIndexAsync(sourceKey, cancellationToken));
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        var sourceKey = Request.Query["source"].FirstOrDefault();
        return View("Edit", _authoringService.GetNew(sourceKey));
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(
        ContentAuthoringEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        model.Document.IsNew = true;
        try
        {
            var created = await _authoringService.CreateAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = created.Slug, source = model.Document.SourceKey });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            _authoringService.PopulateOptions(model);
            return View("Edit", model);
        }
    }

    [HttpGet("{slug}/edit")]
    public async Task<IActionResult> Edit(string slug, CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        var sourceKey = Request.Query["source"].FirstOrDefault() ?? _authoringService.DefaultSourceKey;
        try
        {
            var model = await _authoringService.GetEditAsync(sourceKey, slug, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }
    }

    [HttpPost("{slug}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string slug,
        ContentAuthoringEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        try
        {
            ContentInputValidator.ValidateKey("Route slug", slug);
            model.Document.IsNew = false;
            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = saved.Slug, source = model.Document.SourceKey });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            _authoringService.PopulateOptions(model);
            return View(model);
        }
    }

    [HttpPost("preview")]
    [ValidateAntiForgeryToken]
    public IActionResult Preview(ContentAuthoringEditViewModel model)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        try
        {
            model.RenderedPreviewHtml = _bodyRenderer.Render(model.Document.BodyFormat, model.Document.Body);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        _authoringService.PopulateOptions(model);
        return View("Edit", model);
    }

    [HttpPost("{slug}/copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Copy(
        string slug,
        string source,
        string targetSource,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        try
        {
            await _transferService.CopyAsync(source, targetSource, slug, cancellationToken);
            TempData["ContentAuthoringSuccess"] = $"Synchronized '{slug}' from {source} to {targetSource}.";
            return RedirectToAction(nameof(Index), new { source });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentAuthoringError"] = ex.Message;
            return RedirectToAction(nameof(Index), new { source });
        }
    }

    [HttpPost("copy-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyAll(
        string source,
        string targetSource,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        try
        {
            var copiedCount = await _transferService.CopyAllAsync(source, targetSource, cancellationToken);
            TempData["ContentAuthoringSuccess"] = $"Synchronized {copiedCount} content page(s) from {source} to {targetSource}.";
            return RedirectToAction(nameof(Index), new { source = targetSource });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentAuthoringError"] = ex.Message;
            return RedirectToAction(nameof(Index), new { source });
        }
    }

    [HttpPost("{slug}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(
        string slug,
        string source,
        string targetSource,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        try
        {
            await _authoringService.MoveAsync(source, targetSource, slug, cancellationToken);
            return RedirectToAction(nameof(Index), new { source = targetSource });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ContentAuthoringError"] = ex.Message;
            return RedirectToAction(nameof(Index), new { source });
        }
    }

    private bool IsDevelopmentPreview() => HttpContext.GetSiteModeContext().IsDevelopmentPreview;
}
