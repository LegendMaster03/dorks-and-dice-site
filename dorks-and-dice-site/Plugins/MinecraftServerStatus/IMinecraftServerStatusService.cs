namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

public interface IMinecraftServerStatusService
{
    Task<MinecraftServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
