using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using dorks_and_dice_site.Models.GameServers.Hytale;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.GameServers.Hytale;

public sealed class HytaleServerStatusService : IHytaleServerStatusService, IDisposable
{
    private readonly HytaleServerOptions _options;
    private readonly ILogger<HytaleServerStatusService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private HytaleServerStatus? _cachedStatus;
    private DateTimeOffset _cacheExpiresAt;

    public HytaleServerStatusService(
        IOptions<HytaleServerOptions> options,
        ILogger<HytaleServerStatusService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(
                        new UnixDomainSocketEndPoint(_options.DockerSocketPath),
                        cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    public async Task<HytaleServerStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedStatus is not null && now < _cacheExpiresAt)
        {
            return _cachedStatus;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cachedStatus is not null && now < _cacheExpiresAt)
            {
                return _cachedStatus;
            }

            _cachedStatus = await QueryDockerAsync(cancellationToken);
            _cacheExpiresAt = now.AddSeconds(Math.Max(1, _options.CacheSeconds));
            return _cachedStatus;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<HytaleServerStatus> QueryDockerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var containerName = Uri.EscapeDataString(_options.ContainerName);
            using var response = await _httpClient.GetAsync(
                $"/containers/{containerName}/json",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new HytaleServerStatus(false);
            }

            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            var isRunning = document.RootElement
                .GetProperty("State")
                .GetProperty("Running")
                .GetBoolean();

            return new HytaleServerStatus(isRunning);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or SocketException
            or JsonException
            or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Unable to read Hytale container status from Docker socket {DockerSocketPath}.",
                _options.DockerSocketPath);
            return new HytaleServerStatus(false);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _refreshLock.Dispose();
    }
}
