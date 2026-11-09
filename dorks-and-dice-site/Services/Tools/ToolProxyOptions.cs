namespace dorks_and_dice_site.Services.Tools;

/// <summary>
/// Deployment-configurable request size and timeout limits for Tool upstream
/// proxying. They do not change the Tool identity or per-mode exposure policy.
/// </summary>
public sealed class ToolProxyOptions
{
    public const string SectionName = "ToolHosting:Proxy";
    public const long DefaultMaxRequestBodyBytes = 128L * 1024 * 1024;
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(5);

    public long MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;
}
