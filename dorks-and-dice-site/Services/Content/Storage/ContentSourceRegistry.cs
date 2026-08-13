using dorks_and_dice_site.Models.Site;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public sealed record ContentSourceDefinition(
    string Key,
    string DisplayName,
    string Provider,
    string ConnectionString);

public interface IContentSourceRegistry
{
    string AuthoringSourceKey { get; }
    IReadOnlyList<ContentSourceDefinition> GetDefaultSources(SiteMode siteMode);
    IReadOnlyList<ContentSourceDefinition> GetSourcesByKeys(IEnumerable<string> keys);
    IReadOnlyList<ContentSourceDefinition> GetAllSources();
    ContentSourceDefinition GetSource(string key);
    IReadOnlySet<string> GetKnownSourceKeys();
    void ConfigureDbContext(DbContextOptionsBuilder options, string sourceKey);
}

public sealed class ContentSourceRegistry : IContentSourceRegistry
{
    private readonly IConfiguration _configuration;
    private readonly string _contentRootPath;
    private readonly Dictionary<string, ContentSourceDefinition> _sources;
    private readonly List<string> _sourceOrder;
    private readonly List<string> _globalSources;

    public ContentSourceRegistry(IConfiguration configuration, string contentRootPath)
    {
        _configuration = configuration;
        _contentRootPath = contentRootPath;
        _sources = LoadSources();
        _sourceOrder = _sources.Keys.ToList();
        _globalSources = ReadStringList(configuration.GetSection("ContentStorage:GlobalSources"));
        AuthoringSourceKey = configuration["ContentStorage:AuthoringSource"] ?? "Local";

        _ = GetSource(AuthoringSourceKey);
        ValidateSourceKeys(_globalSources, "ContentStorage:GlobalSources");
    }

    public string AuthoringSourceKey { get; }

    public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(SiteMode siteMode)
    {
        var keys = siteMode switch
        {
            SiteMode.Professional => GetModeSources(SiteMode.Professional),
            SiteMode.DorksAndDice => GetModeSources(SiteMode.DorksAndDice),
            SiteMode.Development => [],
            SiteMode.Unassigned => new List<string>(_globalSources),
            _ => new List<string>(_globalSources)
        };

        return GetSourcesByKeys(keys);
    }

    public IReadOnlyList<ContentSourceDefinition> GetSourcesByKeys(IEnumerable<string> keys)
    {
        var orderedKeys = new List<string>();
        foreach (var key in keys)
        {
            AddDistinct(orderedKeys, key);
        }

        ValidateSourceKeys(orderedKeys, "content source selection");
        return orderedKeys.Select(GetSource).ToList();
    }

    public IReadOnlyList<ContentSourceDefinition> GetAllSources() =>
        _sourceOrder.Select(GetSource).ToList();

    public ContentSourceDefinition GetSource(string key)
    {
        if (_sources.TryGetValue(key, out var source))
        {
            return source;
        }

        throw new InvalidOperationException($"Content source '{key}' is not configured.");
    }

    public IReadOnlySet<string> GetKnownSourceKeys() => new HashSet<string>(_sources.Keys, StringComparer.OrdinalIgnoreCase);

    public void ConfigureDbContext(DbContextOptionsBuilder options, string sourceKey)
    {
        var source = GetSource(sourceKey);
        switch (source.Provider.ToLowerInvariant())
        {
            case "sqlite":
                options.UseSqlite(source.ConnectionString);
                break;
            default:
                throw new NotSupportedException(
                    $"Content database provider '{source.Provider}' for source '{source.Key}' is not supported by this build.");
        }
    }

    private List<string> GetModeSources(SiteMode siteMode)
    {
        var modeSection = _configuration.GetSection($"ContentStorage:Modes:{siteMode}");
        var inheritGlobal = !bool.TryParse(modeSection["InheritGlobal"], out var configuredInheritance)
            || configuredInheritance;
        var keys = inheritGlobal ? new List<string>(_globalSources) : [];

        foreach (var key in ReadStringList(modeSection.GetSection("Remove")))
        {
            keys.RemoveAll(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var key in ReadStringList(modeSection.GetSection("Add")))
        {
            AddDistinct(keys, key);
        }

        ValidateSourceKeys(keys, $"ContentStorage:Modes:{siteMode}");
        return keys;
    }

    private Dictionary<string, ContentSourceDefinition> LoadSources()
    {
        var result = new Dictionary<string, ContentSourceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceSection in _configuration.GetSection("ContentStorage:Sources").GetChildren())
        {
            var key = sourceSection.Key;
            var provider = sourceSection["Provider"]
                ?? throw new InvalidOperationException($"Content source '{key}' does not define a provider.");
            var connectionStringName = sourceSection["ConnectionString"]
                ?? throw new InvalidOperationException($"Content source '{key}' does not define a connection string name.");
            var connectionString = _configuration.GetConnectionString(connectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{connectionStringName}' was not found for content source '{key}'.");

            if (string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqlite = new SqliteConnectionStringBuilder(connectionString);
                if (!Path.IsPathRooted(sqlite.DataSource))
                {
                    sqlite.DataSource = Path.GetFullPath(Path.Combine(_contentRootPath, sqlite.DataSource));
                }
                connectionString = sqlite.ConnectionString;
            }

            result[key] = new ContentSourceDefinition(
                key,
                sourceSection["DisplayName"] ?? key,
                provider,
                connectionString);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("At least one ContentStorage source must be configured.");
        }

        return result;
    }

    private void ValidateSourceKeys(IEnumerable<string> keys, string settingName)
    {
        foreach (var key in keys)
        {
            if (!_sources.ContainsKey(key))
            {
                throw new InvalidOperationException($"{settingName} references unknown content source '{key}'.");
            }
        }
    }

    private static List<string> ReadStringList(IConfigurationSection section) => section
        .GetChildren()
        .Select(child => child.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .ToList();

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
        {
            values.Add(value);
        }
    }
}
