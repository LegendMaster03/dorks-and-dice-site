using dorks_and_dice_site.Models.GameServers;

namespace dorks_and_dice_site.Services.GameServers;

public interface IMinecraftServerStatusService
{
    Task<MinecraftServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
