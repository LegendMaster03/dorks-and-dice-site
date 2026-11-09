namespace dorks_and_dice_site.Plugins.DiscordBot;

/// <summary>
/// Deployment-owned bot credentials, polling interval, and HTTP timeout.
/// A bot token is never portable site-mode or authored-content metadata.
/// </summary>
public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    public bool Enabled { get; init; }
    public string? ClientId { get; init; }
    public string? Token { get; init; }
    public string? TokenFile { get; init; }
    public int ReconcileIntervalSeconds { get; init; } = 60;
    public int RequestTimeoutSeconds { get; init; } = 15;
}

internal sealed record DiscordBotCredential(string Token, string? ClientId);
