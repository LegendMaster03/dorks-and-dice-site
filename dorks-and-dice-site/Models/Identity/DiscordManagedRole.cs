namespace dorks_and_dice_site.Models.Identity;

public sealed class DiscordManagedRole
{
    public const int SourceIdMaxLength = 100;
    public const int GuildIdMaxLength = 32;
    public const int RoleKeyMaxLength = 200;
    public const int DiscordRoleIdMaxLength = 32;
    public const int DisplayNameMaxLength = 100;

    public string SourceId { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public string DiscordRoleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DiscordManagedRoleAssignment
{
    public const int DiscordUserIdMaxLength = 32;

    public string SourceId { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public string DiscordUserId { get; set; } = string.Empty;
    public string DiscordRoleId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DiscordManagedChannel
{
    public const int ChannelKeyMaxLength = 200;
    public const int DiscordChannelIdMaxLength = 32;
    public const int NameMaxLength = 100;

    public string SourceId { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string ChannelKey { get; set; } = string.Empty;
    public string DiscordChannelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public string? ParentKey { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
