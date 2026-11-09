namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

/// <summary>
/// View-component input selecting one display field from the shared status
/// snapshot, not a new network status request.
/// </summary>
public sealed record MinecraftServerStatusFieldViewModel(
    string Field,
    MinecraftServerStatus Status);
