namespace dorks_and_dice_site.Services.Identity;

public sealed class IdentityStorageOptions
{
    public const string SectionName = "IdentityStorage";

    public string Provider { get; set; } = "PostgreSQL";
    public string? ConnectionString { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "dorks_and_dice_identity";
    public string Username { get; set; } = "dorks_and_dice_identity";
    public string? Password { get; set; }
    public string? PasswordFile { get; set; }
}
