using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
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
        "^[\\t ]*(?<directive>\\{\\{[a-z0-9-]+(?:[\\t ]+[a-z][a-z0-9-]*=\"[^\"\\r\\n]*\")*[\\t ]*\\}\\})[\\t ]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private readonly IContentAuthoringService _authoringService;
    private readonly IContentBodyRenderer _bodyRenderer;
    private readonly IContentPageComposer _pageComposer;
    private readonly IContentSourceRegistry _sourceRegistry;

    public ContentAuthoringController(
        IContentAuthoringService authoringService,
        IContentBodyRenderer bodyRenderer,
        IContentPageComposer pageComposer,
        IContentSourceRegistry sourceRegistry)
    {
        _authoringService = authoringService;
        _bodyRenderer = bodyRenderer;
        _pageComposer = pageComposer;
        _sourceRegistry = sourceRegistry;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var sources = ContentAuthoringSourceAccess.GetAccessibleSources(
            _sourceRegistry,
            HttpContext.GetSiteModeContext());
        var sourceModels = await Task.WhenAll(sources.Select(async source => new
        {
            Source = source,
            Model = await _authoringService.GetIndexAsync(source.Key, cancellationToken)
        }));

        var entries = sourceModels
            .SelectMany(sourceModel => sourceModel.Model.Items.Select(item =>
                new ContentAuthoringIndexEntryViewModel
                {
                    Item = item,
                    SourceKey = sourceModel.Source.Key,
                    SourceDisplayName = sourceModel.Source.DisplayName,
                    IsAuthoringSource = string.Equals(
                        sourceModel.Source.Key,
                        _authoringService.DefaultSourceKey,
                        StringComparison.OrdinalIgnoreCase)
                }))
            .OrderBy(entry => entry.Item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SourceDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new ContentAuthoringIndexViewModel
        {
            Entries = entries,
            Items = entries.Select(entry => entry.Item).ToList(),
            SelectedSourceKey = _authoringService.DefaultSourceKey,
            AuthoringSourceKey = _authoringService.DefaultSourceKey,
            Sources = sources.Select(source => new ContentAuthoringSourceOption
            {
                Key = source.Key,
                DisplayName = source.DisplayName
            }).ToList()
        });
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
            return RedirectToAction(nameof(Edit), new
            {
                slug = created.Slug,
                source = model.Document.SourceKey
            });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            _authoringService.PopulateOptions(model);
            return View("Edit", model);
        }
    }

    [HttpGet("{slug}/edit")]
    public async Task<IActionResult> Edit(
        string slug,
        string? source,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceKey = ResolveAccessibleSource(source);
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
        try
        {
            model.Document.SourceKey = ResolveAccessibleSource(model.Document.SourceKey);
            ContentInputValidator.ValidateKey("Route slug", slug);
            model.Document.IsNew = false;
            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return RedirectToAction(nameof(Edit), new
            {
                slug = saved.Slug,
                source = model.Document.SourceKey
            });
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
        try
        {
            model.Document.SourceKey = ResolveAccessibleSource(model.Document.SourceKey);
            var fragments = _pageComposer.Compose(model.Document.BodyFormat, model.Document.Body);
            var preview = new StringBuilder();
            foreach (var fragment in fragments)
            {
                if (fragment.RenderedHtml is not null)
                {
                    preview.Append(fragment.RenderedHtml);
                    continue;
                }

                if (fragment.Component is null)
                {
                    continue;
                }

                var component = HtmlEncoder.Default.Encode(fragment.Component.Name);
                var parameters = fragment.Component.Parameters.Count == 0
                    ? string.Empty
                    : " " + string.Join(
                        " ",
                        fragment.Component.Parameters.Select(parameter =>
                            $"{HtmlEncoder.Default.Encode(parameter.Key)}=&quot;{HtmlEncoder.Default.Encode(parameter.Value)}&quot;"));
                preview.Append(
                    $"<div class=\"alert alert-secondary content-preview-component\" role=\"note\">" +
                    $"Page component: <code>{component}{parameters}</code></div>");
            }

            model.RenderedPreviewHtml = preview.ToString();
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

    private string ResolveAccessibleSource(string? source) =>
        ContentAuthoringSourceAccess.ResolveAccessibleSourceKey(
            _sourceRegistry,
            HttpContext.GetSiteModeContext(),
            source);
}
