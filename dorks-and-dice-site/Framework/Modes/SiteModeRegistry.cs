using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public interface ISiteModeRegistry
{
    IReadOnlyList<SiteModeDefinition> All { get; }
    bool TryGetById(string id, out SiteModeDefinition? definition);
    bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition);
    SiteModeDefinition GetById(string id);
    SiteModeDefinition GetByLegacyMode(SiteMode mode);
}

public sealed class SiteModeRegistry : ISiteModeRegistry
{
    private readonly IReadOnlyDictionary<string, SiteModeDefinition> _byId;
    private readonly IReadOnlyDictionary<SiteMode, SiteModeDefinition> _byLegacyMode;

    public SiteModeRegistry(IEnumerable<SiteModeDefinition> definitions)
    {
        var materialized = definitions.ToArray();
        if (materialized.Length == 0)
        {
            throw new InvalidOperationException("At least one site mode must be registered.");
        }

        var duplicateId = materialized
            .GroupBy(definition => definition.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Duplicate site mode id '{duplicateId.Key}'.");
        }

        var duplicateLegacyMode = materialized
            .Where(definition => definition.LegacyMode.HasValue)
            .GroupBy(definition => definition.LegacyMode!.Value)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateLegacyMode is not null)
        {
            throw new InvalidOperationException($"Duplicate legacy site mode '{duplicateLegacyMode.Key}'.");
        }

        foreach (var definition in materialized)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new InvalidOperationException("Site mode ids can not be empty.");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                throw new InvalidOperationException($"Site mode '{definition.Id}' must have a display name.");
            }
        }

        All = materialized;
        _byId = materialized.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        _byLegacyMode = materialized
            .Where(definition => definition.LegacyMode.HasValue)
            .ToDictionary(definition => definition.LegacyMode!.Value);
    }

    public IReadOnlyList<SiteModeDefinition> All { get; }

    public bool TryGetById(string id, out SiteModeDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        return _byId.TryGetValue(id, out definition);
    }

    public bool TryGetByLegacyMode(SiteMode mode, out SiteModeDefinition? definition) =>
        _byLegacyMode.TryGetValue(mode, out definition);

    public SiteModeDefinition GetById(string id) =>
        TryGetById(id, out var definition)
            ? definition!
            : throw new KeyNotFoundException($"Unknown site mode id '{id}'.");

    public SiteModeDefinition GetByLegacyMode(SiteMode mode) =>
        TryGetByLegacyMode(mode, out var definition)
            ? definition!
            : throw new KeyNotFoundException($"Unknown legacy site mode '{mode}'.");
}
