using System.Text;
using System.Text.Encodings.Web;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers.Operator;

[ApiController]
[Authorize(AuthenticationSchemes = OperatorAuthenticationDefaults.Scheme)]
[ServiceFilter(typeof(OperatorAuditFilter))]
[Route("operator/v1/content")]
public sealed class OperatorContentController(
    IContentAuthoringService authoring,
    IContentAssetService assets,
    IContentPageComposer pageComposer,
    IOperatorContentAccessService access) : ControllerBase
{
    [HttpGet("")]
    [OperatorCapability("content.list")]
    public async Task<IActionResult> List(
        [FromQuery] string? source,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceKey = access.ResolveSource(source);
            var index = await authoring.GetIndexAsync(sourceKey, cancellationToken);
            var items = index.Items
                .Where(item => access.CanEdit(User, item))
                .Where(item => string.IsNullOrWhiteSpace(q)
                    || item.Title.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase)
                    || item.Slug.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase)
                    || item.Summary.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(item => ToListItem(sourceKey, item))
                .ToArray();
            Response.Headers.CacheControl = "no-store";
            return Ok(items);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpGet("{source}/{slug}")]
    [OperatorCapability("content.get")]
    public async Task<IActionResult> Get(
        string source,
        string slug,
        CancellationToken cancellationToken)
    {
        try
        {
            var edit = await GetEditableAsync(source, slug, cancellationToken);
            if (edit is null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "no-store";
            return Ok(ToResponse(edit));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpPost("")]
    [OperatorCapability("content.create")]
    public async Task<IActionResult> Create(
        [FromQuery] string? source,
        OperatorContentWriteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceKey = access.ResolveSource(source);
            var document = ToDocument(sourceKey, request, isNew: true);
            access.PrepareCreate(User, document);
            var item = await authoring.CreateAsync(document, cancellationToken);
            var edit = await authoring.GetEditAsync(sourceKey, item.Slug, cancellationToken)
                ?? throw new InvalidOperationException("The created content could not be reloaded.");
            Response.Headers.CacheControl = "no-store";
            return Created(
                $"/operator/v1/content/{Uri.EscapeDataString(sourceKey)}/{Uri.EscapeDataString(item.Slug)}",
                ToResponse(edit));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpPut("{source}/{slug}")]
    [OperatorCapability("content.save_revision")]
    public async Task<IActionResult> SaveRevision(
        string source,
        string slug,
        OperatorContentWriteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceKey = access.ResolveSource(source);
            var current = await authoring.GetEditAsync(sourceKey, slug, cancellationToken);
            if (current is null || !access.CanEdit(User, current.Document))
            {
                return NotFound();
            }

            var submitted = ToDocument(sourceKey, request, isNew: false);
            submitted.Id = current.Document.Id;
            access.PrepareUpdate(User, submitted, current.Document);
            var item = await authoring.SaveRevisionAsync(submitted, cancellationToken);
            var edit = await authoring.GetEditAsync(sourceKey, item.Slug, cancellationToken)
                ?? throw new InvalidOperationException("The saved content could not be reloaded.");
            Response.Headers.CacheControl = "no-store";
            return Ok(ToResponse(edit));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ContentAuthoringConflictException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ConflictProblem(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpPost("{source}/{slug}/preview")]
    [OperatorCapability("content.preview")]
    public async Task<IActionResult> Preview(
        string source,
        string slug,
        OperatorContentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await GetEditableAsync(source, slug, cancellationToken) is null)
            {
                return NotFound();
            }

            return Ok(new OperatorContentPreviewResponse(RenderPreview(request.BodyFormat, request.Body)));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpGet("{source}/{slug}/media")]
    [OperatorCapability("content.media.list")]
    public async Task<IActionResult> Media(
        string source,
        string slug,
        CancellationToken cancellationToken)
    {
        try
        {
            var edit = await GetEditableAsync(source, slug, cancellationToken);
            if (edit is null)
            {
                return NotFound();
            }

            var sourceKey = access.ResolveSource(source);
            Response.Headers.CacheControl = "no-store";
            return Ok(await assets.GetForPageAsync(sourceKey, slug, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    [HttpPost("{source}/{slug}/media")]
    [OperatorCapability("content.media.upload")]
    public async Task<IActionResult> UploadMedia(
        string source,
        string slug,
        OperatorMediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await GetEditableAsync(source, slug, cancellationToken) is null)
            {
                return NotFound();
            }

            byte[] data;
            try
            {
                data = Convert.FromBase64String(request.Base64Data);
            }
            catch (FormatException)
            {
                return BadRequestProblem("Base64Data is not valid base64.");
            }

            var sourceKey = access.ResolveSource(source);
            await using var stream = new MemoryStream(data, writable: false);
            var uploaded = await assets.UploadAsync(
                sourceKey,
                request.FileName,
                request.MediaType,
                stream,
                data.LongLength,
                cancellationToken);
            await assets.AttachAsync(
                sourceKey,
                slug,
                sourceKey,
                uploaded.AssetKey,
                cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return Ok(uploaded);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequestProblem(exception.Message);
        }
    }

    private async Task<ContentAuthoringEditViewModel?> GetEditableAsync(
        string source,
        string slug,
        CancellationToken cancellationToken)
    {
        var sourceKey = access.ResolveSource(source);
        var edit = await authoring.GetEditAsync(sourceKey, slug, cancellationToken);
        return edit is not null && access.CanEdit(User, edit.Document) ? edit : null;
    }

    private static ContentAuthoringDocument ToDocument(
        string sourceKey,
        OperatorContentWriteRequest request,
        bool isNew) => new()
    {
        IsNew = isNew,
        SourceKey = sourceKey,
        Id = request.Id?.Trim() ?? string.Empty,
        Slug = request.Slug?.Trim() ?? string.Empty,
        ExpectedRevisionId = request.ExpectedRevisionId,
        IsListed = request.IsListed,
        MetadataJson = request.MetadataJson ?? string.Empty,
        TagsText = string.Join(',', request.Tags ?? []),
        VisibleModesSelection = (request.VisibleModes ?? []).ToList(),
        VisibleModesText = string.Join(',', request.VisibleModes ?? []),
        BodyFormat = request.BodyFormat ?? "markdown",
        Body = request.Body ?? string.Empty
    };

    private static OperatorContentListItem ToListItem(string sourceKey, ContentItem item) => new(
        sourceKey,
        item.Id,
        item.Slug,
        item.Title,
        item.Summary,
        item.RevisionId,
        item.IsListed,
        item.Tags,
        item.VisibleInModes);

    private static OperatorContentDocumentResponse ToResponse(ContentAuthoringEditViewModel edit)
    {
        var document = edit.Document;
        return new OperatorContentDocumentResponse(
            document.SourceKey,
            document.Id,
            document.Slug,
            document.ExpectedRevisionId,
            document.IsListed,
            document.MetadataJson,
            ParseList(document.TagsText),
            document.VisibleModesSelection.Count > 0
                ? document.VisibleModesSelection.ToArray()
                : ParseList(document.VisibleModesText),
            document.BodyFormat,
            document.Body,
            edit.History.Select(history => new OperatorContentRevision(
                history.RevisionId,
                history.ParentRevisionId,
                history.CreatedUtc)).ToArray());
    }

    private string RenderPreview(string bodyFormat, string body)
    {
        var fragments = pageComposer.Compose(bodyFormat, body);
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

        return preview.ToString();
    }

    private static string[] ParseList(string? value) =>
        (value ?? string.Empty)
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private ObjectResult BadRequestProblem(string detail) => Problem(
        title: "Invalid Operator content request",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest);

    private ObjectResult ConflictProblem(string detail) => Problem(
        title: "Operator content conflict",
        detail: detail,
        statusCode: StatusCodes.Status409Conflict);
}
