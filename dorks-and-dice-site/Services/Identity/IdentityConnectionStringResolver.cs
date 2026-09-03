using Npgsql;

namespace dorks_and_dice_site.Services.Identity;

public static class IdentityConnectionStringResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("IdentityDatabase");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var section = configuration.GetSection(IdentityStorageOptions.SectionName);
        var directConnectionString = section["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(directConnectionString))
        {
            return directConnectionString;
        }

        var provider = section["Provider"] ?? "PostgreSQL";
        if (!string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Identity storage provider '{provider}' is not supported.");
        }

        var password = section["Password"];
        var passwordFile = section["PasswordFile"];
        if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(passwordFile))
        {
            if (!File.Exists(passwordFile))
            {
                throw new InvalidOperationException($"Identity database password file '{passwordFile}' does not exist.");
            }

            password = File.ReadAllText(passwordFile).Trim();
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Identity database credentials are not configured. Set ConnectionStrings:IdentityDatabase, IdentityStorage:Password, or IdentityStorage:PasswordFile.");
        }

        var port = 5432;
        if (int.TryParse(section["Port"], out var configuredPort))
        {
            port = configuredPort;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = section["Host"] ?? "localhost",
            Port = port,
            Database = section["Database"] ?? "dorks_and_dice_identity",
            Username = section["Username"] ?? "dorks_and_dice_identity",
            Password = password,
            ApplicationName = "dorks-and-dice-site"
        };

        return builder.ConnectionString;
    }
}
