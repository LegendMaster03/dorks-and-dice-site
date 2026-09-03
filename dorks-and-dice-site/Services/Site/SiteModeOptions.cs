namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModeOptions
{
    public const string CanonicalDorksAndDiceHost = "dorks-and-dice.com";
    public const string CanonicalProfessionalHost = "kylebarnett.com";

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
        CanonicalDorksAndDiceHost
    };

    public IReadOnlySet<string> DevelopmentHosts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1"
    };
}
