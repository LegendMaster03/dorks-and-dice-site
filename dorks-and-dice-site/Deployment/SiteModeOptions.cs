namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Deployment composition for mapping public hosts to registered site-mode IDs and
/// identifying hosts that may enter Trusted Preview. Site identities themselves remain
/// defined by <see cref="SiteModeDefinition"/> rather than by deployment hostnames.
/// </summary>
public sealed class SiteModeOptions
{
    // Temporary compatibility constants for consumers that have not yet moved to the
    // stable-ID host APIs below. New code should use GetCanonicalHost/TryGetCanonicalHost.
    public const string CanonicalDorksAndDiceHost = "dorks-and-dice.com";
    public const string CanonicalProfessionalHost = "kylebarnett.com";

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _domainsByModeId;
    private readonly IReadOnlyDictionary<string, string> _canonicalHostsByModeId;

    public SiteModeOptions()
        : this(configuration: null)
    {
    }

    public SiteModeOptions(IConfiguration? configuration)
    {
        var defaults = CreateDefaultModeHosts();
        var domains = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        var canonicalHosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (modeId, defaultHosting) in defaults)
        {
            var section = configuration?.GetSection($"SiteHosting:Modes:{modeId}");
            var canonicalHost = NormalizeHost(section?["CanonicalHost"] ?? defaultHosting.CanonicalHost);
            var configuredDomains = section is null
                ? []
                : ReadStringList(section.GetSection("Domains"));
            var modeDomains = configuredDomains.Count > 0
                ? configuredDomains
                : defaultHosting.Domains.ToList();

            if (!modeDomains.Any(domain => string.Equals(NormalizeHost(domain), canonicalHost, StringComparison.OrdinalIgnoreCase)))
            {
                modeDomains.Add(canonicalHost);
            }

            domains[modeId] = new HashSet<string>(modeDomains.Select(NormalizeHost), StringComparer.OrdinalIgnoreCase);
            canonicalHosts[modeId] = canonicalHost;
        }

        if (configuration is not null)
        {
            foreach (var modeSection in configuration.GetSection("SiteHosting:Modes").GetChildren())
            {
                if (domains.ContainsKey(modeSection.Key))
                {
                    continue;
                }

                var canonicalHostValue = modeSection["CanonicalHost"];
                var configuredDomains = ReadStringList(modeSection.GetSection("Domains"));
                if (string.IsNullOrWhiteSpace(canonicalHostValue) && configuredDomains.Count == 0)
                {
                    continue;
                }

                var canonicalHost = NormalizeHost(canonicalHostValue ?? configuredDomains[0]);
                if (!configuredDomains.Any(domain => string.Equals(NormalizeHost(domain), canonicalHost, StringComparison.OrdinalIgnoreCase)))
                {
                    configuredDomains.Add(canonicalHost);
                }

                domains[modeSection.Key] = new HashSet<string>(configuredDomains.Select(NormalizeHost), StringComparer.OrdinalIgnoreCase);
                canonicalHosts[modeSection.Key] = canonicalHost;
            }
        }

        _domainsByModeId = domains;
        _canonicalHostsByModeId = canonicalHosts;

        var configuredTrustedHosts = configuration is null
            ? []
            : ReadStringList(configuration.GetSection("SiteHosting:TrustedPreviewHosts"));
        DevelopmentHosts = new HashSet<string>(
            (configuredTrustedHosts.Count > 0
                ? configuredTrustedHosts
                : ["localhost", "127.0.0.1", "::1"])
            .Select(NormalizeHost),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> ProfessionalDomains => GetDomains(BuiltInSiteModes.Professional.Id);
    public IReadOnlySet<string> DorksAndDiceDomains => GetDomains(BuiltInSiteModes.DorksAndDice.Id);

    // Compatibility name retained while Trusted Access consumers are migrated.
    public IReadOnlySet<string> DevelopmentHosts { get; }

    public IReadOnlySet<string> GetDomains(string modeId) =>
        _domainsByModeId.TryGetValue(modeId, out var domains)
            ? domains
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool TryGetCanonicalHost(string modeId, out string? canonicalHost)
    {
        if (_canonicalHostsByModeId.TryGetValue(modeId, out var host))
        {
            canonicalHost = host;
            return true;
        }

        canonicalHost = null;
        return false;
    }

    public string GetCanonicalHost(string modeId) =>
        TryGetCanonicalHost(modeId, out var canonicalHost)
            ? canonicalHost!
            : throw new InvalidOperationException($"No canonical host is configured for site mode '{modeId}'.");

    public string? ResolveModeId(string host)
    {
        var normalizedHost = NormalizeHost(host);
        foreach (var (modeId, domains) in _domainsByModeId)
        {
            if (domains.Contains(normalizedHost))
            {
                return modeId;
            }
        }

        return null;
    }

    private static Dictionary<string, ModeHostingDefaults> CreateDefaultModeHosts() => new(StringComparer.OrdinalIgnoreCase)
    {
        [BuiltInSiteModes.Professional.Id] = new(
            CanonicalProfessionalHost,
            [
                "k-barnett.com",
                "kyle-barnett.com",
                "kylebarnett.com",
                "kylebarnett.net",
                "kylebarnett.org",
                "kylebarnett.dev"
            ]),
        [BuiltInSiteModes.DorksAndDice.Id] = new(
            CanonicalDorksAndDiceHost,
            [CanonicalDorksAndDiceHost])
    };

    private static List<string> ReadStringList(IConfigurationSection section) => section
        .GetChildren()
        .Select(child => child.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .ToList();

    private static string NormalizeHost(string host)
    {
        var normalizedHost = host.Trim().ToLowerInvariant();
        return normalizedHost.StartsWith("www.", StringComparison.Ordinal)
            ? normalizedHost[4..]
            : normalizedHost;
    }

    private sealed record ModeHostingDefaults(string CanonicalHost, IReadOnlyList<string> Domains);
}
