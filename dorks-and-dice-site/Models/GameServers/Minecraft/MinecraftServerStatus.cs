namespace dorks_and_dice_site.Models.GameServers.Minecraft;

public sealed record MinecraftServerStatus(
    bool IsOnline,
    string? Motd,
    string? Version,
    int? OnlinePlayers,
    int? MaximumPlayers,
    DateTimeOffset CheckedAt,
    string? Error = null)
{
    public static MinecraftServerStatus Unavailable(string? error = null) =>
        new(false, null, null, null, null, DateTimeOffset.UtcNow, error);
}
