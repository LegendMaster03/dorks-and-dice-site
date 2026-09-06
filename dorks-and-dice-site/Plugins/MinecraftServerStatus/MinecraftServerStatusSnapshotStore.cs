using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

public interface IMinecraftServerStatusSnapshotStore
{
    MinecraftServerStatus Current { get; }
}

public sealed class MinecraftServerStatusSnapshotStore : IMinecraftServerStatusSnapshotStore
{
    private MinecraftServerStatus _current = MinecraftServerStatus.Unavailable("Status has not been checked yet.");

    public MinecraftServerStatus Current => Volatile.Read(ref _current);

    public void Update(MinecraftServerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Volatile.Write(ref _current, status);
    }
}

public sealed class MinecraftServerStatusPoller : BackgroundService
{
    private readonly IMinecraftServerStatusService _statusService;
    private readonly MinecraftServerStatusSnapshotStore _store;
    private readonly MinecraftServerOptions _options;
    private readonly ILogger<MinecraftServerStatusPoller> _logger;

    public MinecraftServerStatusPoller(
        IMinecraftServerStatusService statusService,
        MinecraftServerStatusSnapshotStore store,
        IOptions<MinecraftServerOptions> options,
        ILogger<MinecraftServerStatusPoller> logger)
    {
        _statusService = statusService;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MinecraftServerStatus status;
            try
            {
                status = await _statusService.GetStatusAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Minecraft status polling failure.");
                status = MinecraftServerStatus.Unavailable();
            }

            _store.Update(status);

            var delaySeconds = status.IsOnline
                ? Math.Max(1, _options.CacheSeconds)
                : Math.Max(1, _options.FailureCacheSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
