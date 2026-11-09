namespace dorks_and_dice_site.Services.Identity;

/// <summary>
/// Deployment-owned SMTP settings for shared Identity messages. Sender domain
/// and credentials are resolved per deployment, not embedded in site modes.
/// </summary>
public sealed class AccountEmailOptions
{
    public const string SectionName = "AccountEmail";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PasswordFile { get; set; }
    public string FromLocalPart { get; set; } = "accounts";
}
