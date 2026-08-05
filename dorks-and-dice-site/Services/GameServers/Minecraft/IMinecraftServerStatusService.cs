using dorks_and_dice_site.Models.GameServers.Minecraft;

namespace dorks_and_dice_site.Services.GameServers.Minecraft;

public interface IMinecraftServerStatusService
{
    Task<MinecraftServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
