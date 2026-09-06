using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public abstract class ContentAuthoringControllerBase : Controller
{
    private static readonly Regex DirectiveLinePattern = new(
        "^[\\t ]*(?<directive>\\{\\{[a-z0-9-]+(?:[\\t ]+[a-z][a-z0-9-]*=\"[^\"\\r\\n]*\")*[\\t ]*\\}\\})[\\t ]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly IContentAuthoringService _authoringService;
    private readonly IContentBodyRenderer _bodyRenderer;
    private readonly IContentPageComposer _pageComposer;
    private readonly IContentSourceRegistry _sourceRegistry;

    protected ContentAuthoringControllerBase(
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

    protected abstract bool IsCentralAuthoring { get; }
    protected string RouteBase => IsCentralAuthoring ? "/development/content" : "/editor/content";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var sources = GetSources();
        var sourceModels = await Task.WhenAll(sources.Select(async source => new
        {
            Source = source,
            Model = await _authoringService.GetIndexAsync(source.Key, cancellationToken)
        }));

        IEnumerable<ContentAuthoringIndexEntryViewModel> entries = sourceModels
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
                }));

        string? modeDisplayName = null;
        if (!IsCentralAuthoring)
        {
            var modeContext = HttpContext.GetSiteModeContext();
            var activeModeId = ContentAuthoringModeAccess.RequireActiveModeId(modeContext);
            modeDisplayName = modeContext.ActiveMode?.DisplayName ?? activeModeId;
            entries = entries.Where(entry =>
                ContentAuthoringModeAccess.CanEditItem(User, entry.Item, activeModeId));
        }

        var entryList = entries
            .OrderBy(entry => entry.Item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SourceDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View("~/Views/ContentAuthoring/Index.cshtml", new ContentAuthoringIndexViewModel
        {
            Entries = entryList,
            Items = entryList.Select(entry => entry.Item).ToList(),
            SelectedSourceKey = _authoringService.DefaultSourceKey,
            AuthoringSourceKey = _authoringService.DefaultSourceKey,
            Sources = sources.Select(source => new ContentAuthoringSourceOption
            {
                Key = source.Key,
                DisplayName = source.DisplayName
            }).ToList(),
            RouteBase = RouteBase,
            IsCentralAuthoring = IsCentralAuthoring,
            ModeDisplayName = modeDisplayName
        });
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        var model = _authoringService.GetNew(_authoringService.DefaultSourceKey);
        ConfigureEditModel(model, forceNewMode: true);
        return View("~/Views/ContentAuthoring/Edit.cshtml", model);
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(
        ContentAuthoringEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Document.IsNew = true;
        try
        {
            model.Document.SourceKey = ResolveSource(model.Document.SourceKey);
            if (!IsCentralAuthoring)
            {
                ContentAuthoringModeAccess.ForceNewDocumentMode(
                    model.Document,
                    ContentAuthoringModeAccess.RequireActiveModeId(HttpContext.GetSiteModeContext()));
            }

            var created = await _authoringService.CreateAsync(model.Document, cancellationToken);
            return Redirect(BuildEditUrl(created.Slug, model.Document.SourceKey));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ConfigureEditModel(model, forceNewMode: true);
            return View("~/Views/ContentAuthoring/Edit.cshtml", model);
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
            var sourceKey = ResolveSource(source);
            var model = await _authoringService.GetEditAsync(sourceKey, slug, cancellationToken);
            if (model is null)
            {
                return NotFound();
            }

            if (!CanEditCurrentModel(model))
            {
                return NotFound();
            }

            ConfigureEditModel(model, forceNewMode: false);
            return View("~/Views/ContentAuthoring/Edit.cshtml", model);
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
            model.Document.SourceKey = ResolveSource(model.Document.SourceKey);
            ContentInputValidator.ValidateKey("Route slug", slug);
            model.Document.IsNew = false;

            if (!IsCentralAuthoring)
            {
                var current = await _authoringService.GetEditAsync(
                    model.Document.SourceKey,
                    slug,
                    cancellationToken);
                if (current is null)
                {
                    return NotFound();
                }
                if (!CanEditCurrentModel(current))
                {
                    return NotFound();
                }

                // Stable identity and mode assignment are authority-controlled on the normal editor.
                // A client can not use a crafted form post to re-target another mode.
                model.Document.Id = current.Document.Id;
                ContentAuthoringModeAccess.PreserveExistingDocumentModes(model.Document, current.Document);
            }

            var saved = await _authoringService.SaveRevisionAsync(model.Document, cancellationToken);
            return Redirect(BuildEditUrl(saved.Slug, model.Document.SourceKey));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ConfigureEditModel(model, forceNewMode: false);
            return View("~/Views/ContentAuthoring/Edit.cshtml", model);
        }
    }

    [HttpPost("preview")]
    [ValidateAntiForgeryToken]
    public IActionResult Preview(ContentAuthoringEditViewModel model)
    {
        try
        {
            model.Document.SourceKey = ResolveSource(model.Document.SourceKey);
            if (!IsCentralAuthoring)
            {
                ContentAuthoringModeAccess.ForceNewDocumentMode(
                    model.Document,
                    ContentAuthoringModeAccess.RequireActiveModeId(HttpContext.GetSiteModeContext()));
            }

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

        ConfigureEditModel(model, forceNewMode: model.Document.IsNew);
        return View("~/Views/ContentAuthoring/Edit.cshtml", model);
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

    private IReadOnlyList<ContentSourceDefinition> GetSources() =>
        IsCentralAuthoring
            ? ContentAuthoringSourceAccess.GetCentralSources(_sourceRegistry)
            : ContentAuthoringSourceAccess.GetModeEditorSources(_sourceRegistry);

    private string ResolveSource(string? source) =>
        IsCentralAuthoring
            ? ContentAuthoringSourceAccess.ResolveCentralSourceKey(_sourceRegistry, source)
            : ContentAuthoringSourceAccess.ResolveModeEditorSourceKey(_sourceRegistry, source);

    private bool CanEditCurrentModel(ContentAuthoringEditViewModel model)
    {
        if (IsCentralAuthoring)
        {
            return true;
        }

        var activeModeId = ContentAuthoringModeAccess.RequireActiveModeId(HttpContext.GetSiteModeContext());
        var currentItem = new ContentItem
        {
            VisibleInModes = model.Document.VisibleModesSelection.ToList()
        };
        return ContentAuthoringModeAccess.CanEditItem(User, currentItem, activeModeId);
    }

    private void ConfigureEditModel(ContentAuthoringEditViewModel model, bool forceNewMode)
    {
        _authoringService.PopulateOptions(model);
        model.RouteBase = RouteBase;
        model.IsCentralAuthoring = IsCentralAuthoring;

        if (IsCentralAuthoring)
        {
            model.AllowModeSelection = true;
            model.FixedModeDisplayName = null;
            model.Sources = ContentAuthoringSourceAccess.GetCentralSources(_sourceRegistry)
                .Select(source => new ContentAuthoringSourceOption
                {
                    Key = source.Key,
                    DisplayName = source.DisplayName
                })
                .ToList();
            return;
        }

        var modeContext = HttpContext.GetSiteModeContext();
        var activeModeId = ContentAuthoringModeAccess.RequireActiveModeId(modeContext);
        if (forceNewMode)
        {
            ContentAuthoringModeAccess.ForceNewDocumentMode(model.Document, activeModeId);
        }

        model.AllowModeSelection = false;
        model.FixedModeDisplayName = modeContext.ActiveMode?.DisplayName ?? activeModeId;
        model.Modes =
        [
            new ContentAuthoringModeOption
            {
                Id = activeModeId,
                DisplayName = model.FixedModeDisplayName
            }
        ];
        model.Sources = ContentAuthoringSourceAccess.GetModeEditorSources(_sourceRegistry)
            .Select(source => new ContentAuthoringSourceOption
            {
                Key = source.Key,
                DisplayName = source.DisplayName
            })
            .ToList();
    }

    private string BuildEditUrl(string slug, string source) =>
        $"{RouteBase}/{slug}/edit?source={Uri.EscapeDataString(source)}";
}
