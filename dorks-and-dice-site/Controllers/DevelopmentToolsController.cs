using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/tools")]
public sealed partial class DevelopmentToolsController : Controller
{
    private readonly IToolRegistry _toolRegistry;

    public DevelopmentToolsController(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _toolRegistry = new JsonToolRegistry(environment, configuration);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _toolRegistry.GetAllAsync(cancellationToken));
    }

    [HttpGet("new")]
    public IActionResult Create() => View("Edit", new ToolRegistrationEditViewModel());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetByIdAsync(id, cancellationToken);
        if (tool is null)
        {
            return NotFound();
        }

        return View(new ToolRegistrationEditViewModel
        {
            Id = tool.Id,
            Slug = tool.Slug,
            DisplayName = tool.DisplayName,
            Description = tool.Description,
            IntegrationType = tool.IntegrationType,
            UpstreamBaseUrl = tool.UpstreamBaseUrl,
            FrontendEntryPoint = tool.FrontendEntryPoint,
            HealthPath = tool.HealthPath,
            AllowAnonymous = tool.AllowAnonymous,
            Enabled = tool.Enabled
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ToolRegistrationEditViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);
        Validate(model);
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var existing = model.Id.HasValue
            ? await _toolRegistry.GetByIdAsync(model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && existing is null)
        {
            return NotFound();
        }

        var duplicate = await _toolRegistry.GetBySlugAsync(model.Slug, cancellationToken);
        if (duplicate is not null && duplicate.Id != model.Id)
        {
            ModelState.AddModelError(nameof(model.Slug), "That tool slug is already registered.");
            return View("Edit", model);
        }

        var now = DateTimeOffset.UtcNow;
        var registration = new ToolRegistration
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Slug = model.Slug,
            DisplayName = model.DisplayName,
            Description = model.Description,
            IntegrationType = model.IntegrationType,
            UpstreamBaseUrl = model.UpstreamBaseUrl,
            FrontendEntryPoint = model.FrontendEntryPoint,
            HealthPath = model.HealthPath,
            AllowAnonymous = model.AllowAnonymous,
            Enabled = model.Enabled,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };

        await _toolRegistry.SaveAsync(registration, cancellationToken);
        TempData["DevelopmentToolMessage"] = existing is null ? "Tool registered." : "Tool registration updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _toolRegistry.DeleteAsync(id, cancellationToken);
        TempData["DevelopmentToolMessage"] = "Tool registration removed.";
        return RedirectToAction(nameof(Index));
    }

    private void Validate(ToolRegistrationEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Slug) || !ToolSlugRegex().IsMatch(model.Slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug must contain only lowercase letters, numbers, and hyphens.");
        }

        if (!string.IsNullOrWhiteSpace(model.UpstreamBaseUrl)
            && (!Uri.TryCreate(model.UpstreamBaseUrl, UriKind.Absolute, out var upstream)
                || (upstream.Scheme != Uri.UriSchemeHttp && upstream.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(upstream.UserInfo)))
        {
            ModelState.AddModelError(nameof(model.UpstreamBaseUrl), "Upstream base URL must be an absolute HTTP or HTTPS URL without embedded credentials.");
        }

        if (!string.IsNullOrWhiteSpace(model.FrontendEntryPoint)
            && !model.FrontendEntryPoint.StartsWith("/", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.FrontendEntryPoint), "Frontend entry point must be an absolute path beginning with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(model.HealthPath)
            && !model.HealthPath.StartsWith("/", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.HealthPath), "Health path must begin with '/'.");
        }
    }

    private static void Normalize(ToolRegistrationEditViewModel model)
    {
        model.Slug = model.Slug.Trim().ToLowerInvariant();
        model.DisplayName = model.DisplayName.Trim();
        model.Description = NullIfWhiteSpace(model.Description);
        model.UpstreamBaseUrl = NullIfWhiteSpace(model.UpstreamBaseUrl)?.TrimEnd('/');
        model.FrontendEntryPoint = NullIfWhiteSpace(model.FrontendEntryPoint);
        model.HealthPath = NullIfWhiteSpace(model.HealthPath);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolSlugRegex();
}
