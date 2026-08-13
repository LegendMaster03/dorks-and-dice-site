using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("development/content")]
public sealed class ContentAuthoringController : Controller
{
    private readonly IContentAuthoringService _authoringService;
    private readonly IContentBodyRenderer _bodyRenderer;

    public ContentAuthoringController(
        IContentAuthoringService authoringService,
        IContentBodyRenderer bodyRenderer)
    {
        _authoringService = authoringService;
        _bodyRenderer = bodyRenderer;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        return View(await _authoringService.GetIndexAsync(cancellationToken));
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        if (!IsDevelopmentPreview())
        {
            return NotFound();
        }

        return View("Edit", _authoringService.GetNew());
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
            return RedirectToAction(nameof(Edit), new { slug = created.Slug });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
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

        var model = await _authoringService.GetEditAsync(slug, cancellationToken);
        return model is null ? NotFound() : View(model);
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

        model.Document.IsNew = false;
        try
        {
            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = saved.Slug });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
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

        return View("Edit", model);
    }

    private bool IsDevelopmentPreview() => HttpContext.GetSiteModeContext().IsDevelopmentPreview;
}
