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
            Key = tool.Key,
            Kind = tool.Kind,
            Slug = tool.Slug,
            DisplayName = tool.DisplayName,
            Description = tool.Description,
            IntegrationType = tool.IntegrationType,
            IntegrationContractVersion = tool.IntegrationContractVersion,
            UpstreamBaseUrl = tool.UpstreamBaseUrl,
            FrontendEntryPoint = tool.FrontendEntryPoint,
            HealthPath = tool.HealthPath,
            Modes = tool.Modes?.ToList() ?? [],
            DelegationTargets = tool.DelegationTargets?.ToList() ?? [],
            DelegationTargetsText = string.Join(
                Environment.NewLine,
                tool.DelegationTargets ?? []),
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

        if (existing is not null
            && !string.Equals(existing.Key, model.Key, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Key), "The stable registration key can not be changed after creation.");
            return View("Edit", model);
        }

        var duplicateKey = await _toolRegistry.GetByKeyAsync(model.Key, cancellationToken);
        if (duplicateKey is not null && duplicateKey.Id != model.Id)
        {
            ModelState.AddModelError(nameof(model.Key), "That registration key is already registered.");
            return View("Edit", model);
        }

        if (model.Kind == ToolKind.Application && !string.IsNullOrWhiteSpace(model.Slug))
        {
            var duplicateSlug = await _toolRegistry.GetBySlugAsync(model.Slug, cancellationToken);
            if (duplicateSlug is not null && duplicateSlug.Id != model.Id)
            {
                ModelState.AddModelError(nameof(model.Slug), "That public Tool slug is already registered.");
                return View("Edit", model);
            }
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
            Key = model.Key,
            Kind = model.Kind,
            Slug = model.Kind == ToolKind.Application ? model.Slug : null,
            DisplayName = model.DisplayName,
            Description = model.Description,
            IntegrationType = model.Kind == ToolKind.Application ? model.IntegrationType : null,
            IntegrationContractVersion = model.Kind == ToolKind.Application
                && model.IntegrationType == ToolIntegrationType.EmbeddedModule
                    ? model.IntegrationContractVersion
                    : null,
            UpstreamBaseUrl = model.UpstreamBaseUrl,
            FrontendEntryPoint = model.Kind == ToolKind.Application ? model.FrontendEntryPoint : null,
            HealthPath = model.HealthPath,
            Modes = modes,
            DelegationTargets = model.DelegationTargets.ToList(),
            AllowAnonymous = model.Kind == ToolKind.Application && model.AllowAnonymous,
            Enabled = model.Enabled,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };

        await _toolRegistry.SaveAsync(registration, cancellationToken);
        TempData["DevelopmentToolMessage"] = existing is null
            ? "Tool registration created."
            : "Tool registration updated.";
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

        if (string.IsNullOrWhiteSpace(model.Key) || !ToolKeyRegex().IsMatch(model.Key))
        {
            ModelState.AddModelError(nameof(model.Key), "Registration key must contain only lowercase letters, numbers, and hyphens.");
        }

        if (model.Kind == ToolKind.Application)
        {
            if (string.IsNullOrWhiteSpace(model.Slug) || !ToolKeyRegex().IsMatch(model.Slug))
            {
                ModelState.AddModelError(nameof(model.Slug), "Public slug must contain only lowercase letters, numbers, and hyphens.");
            }

            var contractError = ToolIntegrationContractPolicy.GetUnsupportedReason(
                model.Kind,
                model.IntegrationType,
                model.IntegrationContractVersion);
            if (contractError is not null)
            {
                ModelState.AddModelError(nameof(model.IntegrationContractVersion), contractError);
            }
        }
        else if (string.IsNullOrWhiteSpace(model.UpstreamBaseUrl))
        {
            ModelState.AddModelError(nameof(model.UpstreamBaseUrl), "Headless services require an upstream base URL.");
        }

        if (model.Modes.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Modes), "Select at least one site mode for this registration.");
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

        var invalidDelegationTarget = model.DelegationTargets
            .FirstOrDefault(target => !ToolKeyRegex().IsMatch(target));
        if (invalidDelegationTarget is not null)
        {
            ModelState.AddModelError(
                nameof(model.DelegationTargetsText),
                "Delegation targets must be valid lowercase registration keys.");
        }

        if (model.DelegationTargets.Contains(model.Key, StringComparer.Ordinal))
        {
            ModelState.AddModelError(
                nameof(model.DelegationTargetsText),
                "A Tool can not delegate to itself.");
        }

        if (!_upstreamPolicy.IsAllowed(model.UpstreamBaseUrl, out var upstreamReason))
        {
            ModelState.AddModelError(
                nameof(model.UpstreamBaseUrl),
                upstreamReason ?? "Upstream base URL is not allowed.");
        }

        if (model.Kind == ToolKind.Application
            && !string.IsNullOrWhiteSpace(model.FrontendEntryPoint)
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
        model.Key = (model.Key ?? string.Empty).Trim().ToLowerInvariant();
        model.Slug = NullIfWhiteSpace(model.Slug)?.ToLowerInvariant();
        if (model.Kind == ToolKind.Application
            && string.IsNullOrWhiteSpace(model.Key)
            && !string.IsNullOrWhiteSpace(model.Slug))
        {
            model.Key = model.Slug;
        }

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
        model.DelegationTargets = (model.DelegationTargetsText ?? string.Empty)
            .Split(
                [',', ';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(target => target.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToList();
        model.DelegationTargetsText = string.Join(
            Environment.NewLine,
            model.DelegationTargets);

        if (model.Kind == ToolKind.Service)
        {
            model.Slug = null;
            model.IntegrationType = null;
            model.IntegrationContractVersion = null;
            model.FrontendEntryPoint = null;
            model.AllowAnonymous = false;
        }
        else if (model.IntegrationType != ToolIntegrationType.EmbeddedModule)
        {
            model.IntegrationContractVersion = null;
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolKeyRegex();
}
