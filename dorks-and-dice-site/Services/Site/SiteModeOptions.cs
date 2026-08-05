namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeOptions
{
    public IReadOnlySet<string> ProfessionalDomains { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "k-barnett.com",
        "kyle-barnett.com",
        "kylebarnett.com",
        "kylebarnett.net",
        "kylebarnett.org",
        "kylebarnett.dev"
    };

    public IReadOnlySet<string> DorksAndDiceDomains { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dorks-and-dice.com"
    };

    public IReadOnlySet<string> DevelopmentHosts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "10.0.0.7"
    };
}
