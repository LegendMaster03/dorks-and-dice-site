namespace dorks_and_dice_site.Plugins.MinecraftServerStatus;

public sealed class MinecraftServerOptions
{
    public const string SectionName = "GameServers:Minecraft";

    public string Host { get; set; } = "10.0.0.7";
    public int Port { get; set; } = 25565;
    public int ProtocolVersion { get; set; } = 776;
    public int TimeoutMilliseconds { get; set; } = 2000;
    public int CacheSeconds { get; set; } = 30;
    public int FailureCacheSeconds { get; set; } = 10;
}
