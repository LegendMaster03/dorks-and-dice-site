using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.ModeEditor)]
[Route("editor/content")]
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
        var model = await _authoringService.GetIndexAsync(
            _authoringService.DefaultSourceKey,
            cancellationToken);

        if (!IsGlobalEditor())
        {
            var editorMode = GetScopedEditorMode();
            model.Items.RemoveAll(item =>
                item.VisibleInModes.Count != 1 || item.VisibleInModes[0] != editorMode);
        }

        return View(model);
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        var model = _authoringService.GetNew(_authoringService.DefaultSourceKey);
        ApplyEditorScope(model.Document);
        _authoringService.PopulateOptions(model);
        return View("Edit", model);
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(
        ContentAuthoringEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Document.IsNew = true;
        model.Document.SourceKey = _authoringService.DefaultSourceKey;
        ApplyEditorScope(model.Document);

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
            if (model is null)
            {
                return NotFound();
            }

            if (!CanEditDocument(model.Document))
            {
                return Forbid();
            }

            return View(model);
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
        try
        {
            ContentInputValidator.ValidateKey("Route slug", slug);
            var existing = await _authoringService.GetEditAsync(
                _authoringService.DefaultSourceKey,
                slug,
                cancellationToken);
            if (existing is null)
            {
                return NotFound();
            }
            if (!CanEditDocument(existing.Document))
            {
                return Forbid();
            }
            if (!string.Equals(existing.Document.Id, model.Document.Id, StringComparison.Ordinal))
            {
                return BadRequest();
            }

            model.Document.IsNew = false;
            model.Document.SourceKey = _authoringService.DefaultSourceKey;
            ApplyEditorScope(model.Document);
            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new { slug = saved.Slug });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Document.SourceKey = _authoringService.DefaultSourceKey;
            ApplyEditorScope(model.Document);
            _authoringService.PopulateOptions(model);
            return View(model);
        }
    }

    [HttpPost("preview")]
    [ValidateAntiForgeryToken]
    public IActionResult Preview(ContentAuthoringEditViewModel model)
    {
        model.Document.SourceKey = _authoringService.DefaultSourceKey;
        ApplyEditorScope(model.Document);

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

    private bool IsGlobalEditor() => User.IsInRole(AccountRoles.Admin);

    private SiteMode GetScopedEditorMode()
    {
        var mode = HttpContext.GetSiteModeContext().SiteMode;
        return mode is SiteMode.DorksAndDice or SiteMode.Professional
            ? mode
            : throw new InvalidOperationException("A scoped editor must use a public site mode.");
    }

    private bool CanEditDocument(ContentAuthoringDocument document)
    {
        if (IsGlobalEditor())
        {
            return true;
        }

        var values = document.VisibleModesSelection.Count > 0
            ? document.VisibleModesSelection
            : document.VisibleModesText.Split(
                [',', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (values.Count != 1
            || !Enum.TryParse<SiteMode>(values[0], ignoreCase: true, out var documentMode))
        {
            return false;
        }

        return documentMode == GetScopedEditorMode();
    }

    private void ApplyEditorScope(ContentAuthoringDocument document)
    {
        document.SourceKey = _authoringService.DefaultSourceKey;
        if (IsGlobalEditor())
        {
            return;
        }

        var modeName = GetScopedEditorMode().ToString();
        document.VisibleModesText = modeName;
        document.VisibleModesSelection = [modeName];
    }
}
