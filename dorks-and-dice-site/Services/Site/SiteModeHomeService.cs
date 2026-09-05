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
/// Resolves the home-page implementation for the active normal mode. A mode that does not
/// provide a home module falls back at the component boundary instead of requiring a shared
/// controller switch for every registered mode.
/// </summary>
public sealed class SiteModeHomeService : ISiteModeHomeService
{
    private readonly IReadOnlyDictionary<string, ISiteModeHomeModule> _modules;
    private readonly ISiteModeHomeModule _fallbackModule;

    public SiteModeHomeService(IEnumerable<ISiteModeHomeModule> modules)
    {
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

    public Task<SiteModeHomeResult> GetHomeAsync(
        SiteModeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = context.ActiveModeId ?? FrameworkRuntimeStates.Fallback.Id;
        var module = _modules.GetValueOrDefault(key) ?? _fallbackModule;
        return module.BuildAsync(cancellationToken);
    }
}
