using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

public sealed class MinecraftServerStatusController : Controller
{
    private readonly IMinecraftServerStatusSnapshotStore _store;
    private readonly MinecraftServerOptions _options;

    public MinecraftServerStatusController(
        IMinecraftServerStatusSnapshotStore store,
        IOptions<MinecraftServerOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Snapshot()
    {
        var status = _store.Current;
        return Json(new
        {
            status.IsOnline,
            status.Motd,
            status.Version,
            status.OnlinePlayers,
            status.MaximumPlayers,
            status.CheckedAt,
            RefreshAfterMilliseconds = Math.Max(1, _options.ClientRefreshSeconds) * 1000
        });
    }
}
