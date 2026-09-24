namespace dorks_and_dice_site.Plugins.Discord;

/// <summary>
/// Stable provider identity shared by Discord-facing plugins. Account linking and
/// mode-scoped Discord communities are separate capabilities that use the same provider key.
/// </summary>
public static class DiscordProvider
{
    public const string Id = "discord";
    public const string DisplayName = "Discord";
}
