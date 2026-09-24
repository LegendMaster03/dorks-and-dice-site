namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Describes one external resource/community attached to a normal Site mode.
/// The same external resource may intentionally be referenced by more than one mode.
/// </summary>
public sealed record ModeExternalConnection(
    string ModeId,
    string ProviderId,
    string ResourceId);

public interface IModeExternalConnectionRegistry
{
    IReadOnlyList<ModeExternalConnection> All { get; }

    IReadOnlyList<ModeExternalConnection> GetForMode(string modeId);

    bool TryGet(
        string modeId,
        string providerId,
        out ModeExternalConnection? connection);
}

/// <summary>
/// Reads deployment-owned external-community bindings from configuration. Account identity
/// remains global; this registry only answers which external resource belongs to which mode.
/// </summary>
public sealed class ConfigurationModeExternalConnectionRegistry
    : IModeExternalConnectionRegistry
{
    public const string SectionName = "ModeConnections";

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyDictionary<string, ModeExternalConnection>> _byMode;

    public ConfigurationModeExternalConnectionRegistry(
        IConfiguration configuration,
        ISiteModeRegistry siteModeRegistry)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(siteModeRegistry);

        var all = new List<ModeExternalConnection>();
        var byMode = new Dictionary<
            string,
            IReadOnlyDictionary<string, ModeExternalConnection>>(
                StringComparer.Ordinal);

        foreach (var modeSection in configuration.GetSection(SectionName).GetChildren())
        {
            if (!siteModeRegistry.TryGetById(modeSection.Key, out _))
            {
                throw new InvalidOperationException(
                    $"ModeConnections contains unknown site mode '{modeSection.Key}'.");
            }

            var providers = new Dictionary<string, ModeExternalConnection>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var providerSection in modeSection.GetChildren())
            {
                var providerId = providerSection.Key;
                ValidateProviderId(providerId);

                var resourceId = providerSection["ResourceId"]?.Trim();
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    throw new InvalidOperationException(
                        $"Mode connection '{modeSection.Key}:{providerId}' requires ResourceId.");
                }

                var connection = new ModeExternalConnection(
                    modeSection.Key,
                    providerId,
                    resourceId);

                if (!providers.TryAdd(providerId, connection))
                {
                    throw new InvalidOperationException(
                        $"Duplicate mode connection '{modeSection.Key}:{providerId}'.");
                }

                all.Add(connection);
            }

            byMode[modeSection.Key] = providers;
        }

        All = all.AsReadOnly();
        _byMode = byMode;
    }

    public IReadOnlyList<ModeExternalConnection> All { get; }

    public IReadOnlyList<ModeExternalConnection> GetForMode(string modeId)
    {
        if (string.IsNullOrWhiteSpace(modeId)
            || !_byMode.TryGetValue(modeId, out var providers))
        {
            return [];
        }

        return providers.Values.ToArray();
    }

    public bool TryGet(
        string modeId,
        string providerId,
        out ModeExternalConnection? connection)
    {
        connection = null;
        if (string.IsNullOrWhiteSpace(modeId)
            || string.IsNullOrWhiteSpace(providerId)
            || !_byMode.TryGetValue(modeId, out var providers))
        {
            return false;
        }

        return providers.TryGetValue(providerId, out connection);
    }

    private static void ValidateProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)
            || !string.Equals(
                providerId,
                providerId.ToLowerInvariant(),
                StringComparison.Ordinal)
            || providerId.Any(character =>
                !(character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-'))
            || providerId[0] == '-'
            || providerId[^1] == '-')
        {
            throw new InvalidOperationException(
                $"Mode connection provider ID '{providerId}' must use lowercase letters, numbers, and internal hyphens only.");
        }
    }
}
