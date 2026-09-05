using System.Text.Json;
using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public interface IToolRegistry
{
    Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ToolRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ToolRegistration?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SaveAsync(ToolRegistration registration, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class JsonToolRegistry : IToolRegistry
{
    public const string RegistryPathConfigurationKey = "ToolHosting:RegistryPath";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string _registryPath;

    public JsonToolRegistry(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration[RegistryPathConfigurationKey] ?? "Content/tool-registry.json";
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"{RegistryPathConfigurationKey} must not be empty.");
        }

        _registryPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public async Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken))
                .OrderBy(tool => tool.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<ToolRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken)).FirstOrDefault(tool => tool.Id == id);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<ToolRegistration?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken)).FirstOrDefault(tool =>
                string.Equals(tool.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveAsync(ToolRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var tools = await ReadUnsafeAsync(cancellationToken);
            var duplicate = tools.FirstOrDefault(tool =>
                tool.Id != registration.Id
                && string.Equals(tool.Slug, registration.Slug, StringComparison.OrdinalIgnoreCase));
            if (duplicate is not null)
            {
                throw new InvalidOperationException($"A tool with slug '{registration.Slug}' already exists.");
            }

            var index = tools.FindIndex(tool => tool.Id == registration.Id);
            if (index >= 0)
            {
                tools[index] = registration;
            }
            else
            {
                tools.Add(registration);
            }

            await WriteUnsafeAsync(tools, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var tools = await ReadUnsafeAsync(cancellationToken);
            var removed = tools.RemoveAll(tool => tool.Id == id) > 0;
            if (removed)
            {
                await WriteUnsafeAsync(tools, cancellationToken);
            }

            return removed;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<List<ToolRegistration>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_registryPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_registryPath);
        return await JsonSerializer.DeserializeAsync<List<ToolRegistration>>(stream, JsonOptions, cancellationToken)
            ?? [];
    }

    private async Task WriteUnsafeAsync(List<ToolRegistration> tools, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_registryPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, tools, JsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, _registryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
