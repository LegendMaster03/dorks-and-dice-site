using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Identity;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Authorize(Policy = AuthorizationPolicies.DevAccess)]
[Route("development/tools")]
public sealed partial class DevelopmentToolsController : Controller
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolHealthService _toolHealthService;
    private readonly IToolUpstreamPolicy _upstreamPolicy;
    private readonly ISiteModeRegistry _siteModeRegistry;

    public DevelopmentToolsController(
        IToolRegistry toolRegistry,
        IToolHealthService toolHealthService,
        IToolUpstreamPolicy upstreamPolicy,
        ISiteModeRegistry siteModeRegistry)
    {
        _toolRegistry = toolRegistry;
        _toolHealthService = toolHealthService;
        _upstreamPolicy = upstreamPolicy;
        _siteModeRegistry = siteModeRegistry;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tools = await _toolRegistry.GetAllAsync(cancellationToken);
        var items = await Task.WhenAll(tools.Select(async tool => new DevelopmentToolListItemViewModel
        {
            Tool = tool,
            Health = await _toolHealthService.CheckAsync(tool, cancellationToken)
        }));
        return View(items);
    }

    [HttpGet("new")]
    public IActionResult Create() =>
        View("Edit", PopulateModeOptions(new ToolRegistrationEditViewModel()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var tool = await _toolRegistry.GetByIdAsync(id, cancellationToken);
        if (tool is null)
        {
            return NotFound();
        }

        return View(PopulateModeOptions(new ToolRegistrationEditViewModel
        {
            Id = tool.Id,
            Slug = tool.Slug,
            DisplayName = tool.DisplayName,
            Description = tool.Description,
            IntegrationType = tool.IntegrationType,
            UpstreamBaseUrl = tool.UpstreamBaseUrl,
            FrontendEntryPoint = tool.FrontendEntryPoint,
            HealthPath = tool.HealthPath,
            Modes = tool.Modes?.ToList() ?? [],
            AllowAnonymous = tool.AllowAnonymous,
            Enabled = tool.Enabled
        }));
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ToolRegistrationEditViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model);
        PopulateModeOptions(model);
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

        var selectedModes = model.Modes.ToHashSet(StringComparer.Ordinal);
        var modes = _siteModeRegistry.All
            .Where(mode => selectedModes.Contains(mode.Id))
            .Select(mode => mode.Id)
            .ToList();

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
            Modes = modes,
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

    private ToolRegistrationEditViewModel PopulateModeOptions(ToolRegistrationEditViewModel model)
    {
        model.ModeOptions = _siteModeRegistry.All
            .Select(mode => new ToolModeOptionViewModel
            {
                Id = mode.Id,
                DisplayName = mode.DisplayName
            })
            .ToList();
        return model;
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

        if (model.Modes.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Modes), "Select at least one site mode for this tool.");
        }
        else
        {
            var unknownModes = model.Modes
                .Where(modeId => !_siteModeRegistry.TryGetById(modeId, out _))
                .ToArray();
            if (unknownModes.Length > 0)
            {
                ModelState.AddModelError(nameof(model.Modes), "One or more selected site modes are not registered.");
            }
        }

        if (!_upstreamPolicy.IsAllowed(model.UpstreamBaseUrl, out var upstreamReason))
        {
            ModelState.AddModelError(
                nameof(model.UpstreamBaseUrl),
                upstreamReason ?? "Upstream base URL is not allowed.");
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
        model.Slug = (model.Slug ?? string.Empty).Trim().ToLowerInvariant();
        model.DisplayName = (model.DisplayName ?? string.Empty).Trim();
        model.Description = NullIfWhiteSpace(model.Description);
        model.UpstreamBaseUrl = NullIfWhiteSpace(model.UpstreamBaseUrl)?.TrimEnd('/');
        model.FrontendEntryPoint = NullIfWhiteSpace(model.FrontendEntryPoint);
        model.HealthPath = NullIfWhiteSpace(model.HealthPath);
        model.Modes = (model.Modes ?? [])
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .Select(mode => mode.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolSlugRegex();
}
