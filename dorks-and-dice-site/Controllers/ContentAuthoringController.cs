using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.ModeEditor)]
[Route("editor/content")]
[Route("development/content")]
[RequestSizeLimit(ContentInputPolicy.MaxAuthoringRequestBytes)]
public sealed class ContentAuthoringController : Controller
{
    private static readonly Regex DirectiveLinePattern = new(
        @"^[\t ]*(?<directive>\{\{[a-z0-9-]+\}\})[\t ]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
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
        return View(await _authoringService.GetIndexAsync(_authoringService.DefaultSourceKey, cancellationToken));
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        return View("Edit", _authoringService.GetNew(_authoringService.DefaultSourceKey));
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(
        ContentAuthoringEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Document.IsNew = true;
        model.Document.SourceKey = _authoringService.DefaultSourceKey;
        try
        {
            var created = await _authoringService.CreateAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = created.Slug });
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
        try
        {
            var model = await _authoringService.GetEditAsync(
                _authoringService.DefaultSourceKey,
                slug,
                cancellationToken);
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
        model.Document.SourceKey = _authoringService.DefaultSourceKey;
        try
        {
            ContentInputValidator.ValidateKey("Route slug", slug);
            model.Document.IsNew = false;
            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = saved.Slug });
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
        model.Document.SourceKey = _authoringService.DefaultSourceKey;
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

    [HttpPost("visual/render")]
    [ValidateAntiForgeryToken]
    public IActionResult RenderVisual([FromForm] string? body)
    {
        body ??= string.Empty;

        var directives = new List<string>();
        var protectedMarkdown = DirectiveLinePattern.Replace(body, match =>
        {
            var index = directives.Count;
            directives.Add(match.Groups["directive"].Value);
            return $"VISUALDIRECTIVEPLACEHOLDER{index}END";
        });

        var html = _bodyRenderer.Render("markdown", protectedMarkdown);
        for (var index = 0; index < directives.Count; index++)
        {
            var marker = $"VISUALDIRECTIVEPLACEHOLDER{index}END";
            var directive = HtmlEncoder.Default.Encode(directives[index]);
            html = html.Replace(
                $"<p>{marker}</p>",
                $"<div class=\"content-visual-directive\" contenteditable=\"false\" data-directive=\"{directive}\">{directive}</div>",
                StringComparison.Ordinal);
        }

        return Json(new { html });
    }
}
