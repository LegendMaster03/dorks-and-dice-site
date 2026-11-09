namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

/// <summary>
/// Reads the configured Minecraft server's current status; callers do not
/// select arbitrary hosts through authored page content.
/// </summary>
public interface IMinecraftServerStatusService
{
    Task<MinecraftServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
