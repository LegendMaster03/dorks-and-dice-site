namespace dorks_and_dice_site.Services.Identity;

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
