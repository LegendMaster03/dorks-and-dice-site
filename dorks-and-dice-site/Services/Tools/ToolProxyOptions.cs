namespace dorks_and_dice_site.Services.Tools;

public sealed class ToolProxyOptions
{
    public const string SectionName = "ToolHosting:Proxy";
    public const long DefaultMaxRequestBodyBytes = 128L * 1024 * 1024;
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(5);

    public long MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;
}
