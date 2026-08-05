using dorks_and_dice_site.Models.GameServers.Hytale;

namespace dorks_and_dice_site.Services.GameServers.Hytale;

public interface IHytaleServerStatusService
{
    Task<HytaleServerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
