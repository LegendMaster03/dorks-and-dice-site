using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Services.Site;

public sealed record SiteModeHomeResult(
    string ViewPath,
    object? Model = null,
    IReadOnlyDictionary<string, object?>? ViewData = null);

public interface ISiteModeHomeModule
{
    string HomeKey { get; }
    Task<SiteModeHomeResult> BuildAsync(CancellationToken cancellationToken = default);
}

public interface ISiteModeHomeService
{
    Task<SiteModeHomeResult> GetHomeAsync(
        SiteModeContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves a normal mode's homepage. Database-backed homepage content is the shared path for
/// every normal mode. Existing mode home modules remain as migration fallbacks until their
/// current homepage content has been moved into the content system.
/// </summary>
public sealed class SiteModeHomeService : ISiteModeHomeService
{
    private readonly IHomepageContentService _homepageContentService;
    private readonly IReadOnlyDictionary<string, ISiteModeHomeModule> _modules;
    private readonly ISiteModeHomeModule _fallbackModule;

    public SiteModeHomeService(
        IHomepageContentService homepageContentService,
        IEnumerable<ISiteModeHomeModule> modules)
    {
        _homepageContentService = homepageContentService;

        var materialized = modules.ToArray();
        var duplicateKey = materialized
            .GroupBy(module => module.HomeKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"Duplicate home module key '{duplicateKey.Key}'.");
        }

        _modules = materialized.ToDictionary(module => module.HomeKey, StringComparer.Ordinal);
        _fallbackModule = _modules.TryGetValue(FrameworkRuntimeStates.Fallback.Id, out var fallback)
            ? fallback
            : throw new InvalidOperationException(
                $"Framework fallback home module '{FrameworkRuntimeStates.Fallback.Id}' is not registered.");
    }

    public async Task<SiteModeHomeResult> GetHomeAsync(
        SiteModeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ActiveMode is not null)
        {
            var homepage = await _homepageContentService.GetAsync(context, cancellationToken);
            if (homepage is not null)
            {
                return new SiteModeHomeResult(
                    "~/Views/Content/Homepage.cshtml",
                    homepage);
            }
        }

        var key = context.ActiveModeId ?? FrameworkRuntimeStates.Fallback.Id;
        var module = _modules.GetValueOrDefault(key) ?? _fallbackModule;
        return await module.BuildAsync(cancellationToken);
    }
}
