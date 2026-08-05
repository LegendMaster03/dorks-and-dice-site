namespace dorks_and_dice_site.Services.GameServers.Hytale;

public sealed class HytaleServerOptions
{
    public const string SectionName = "GameServers:Hytale";

    public string ContainerName { get; set; } = "hytale";

    public string DockerSocketPath { get; set; } = "/var/run/docker.sock";

    public int CacheSeconds { get; set; } = 15;
}
