using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorProvisioningHostedService(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<OperatorProvisioningHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (args.Length == 0 || !string.Equals(args[0], "operator", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await using var scope = services.CreateAsyncScope();
            if (args.Length >= 2 && string.Equals(args[1], "create", StringComparison.OrdinalIgnoreCase))
            {
                await CreateServicePrincipalAsync(scope.ServiceProvider, args[2..], cancellationToken);
            }
            else if (args.Length >= 3
                && string.Equals(args[1], "credential", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[2], "create", StringComparison.OrdinalIgnoreCase))
            {
                await CreateCredentialAsync(scope.ServiceProvider, args[3..], cancellationToken);
            }
            else if (args.Length >= 3
                && string.Equals(args[1], "credential", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[2], "revoke", StringComparison.OrdinalIgnoreCase))
            {
                await RevokeCredentialAsync(scope.ServiceProvider, args[3..], cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(Usage);
            }
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            logger.LogError(exception, "Operator provisioning failed.");
            Console.Error.WriteLine(exception.Message);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task CreateServicePrincipalAsync(
        IServiceProvider services,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = ParseCreateOptions(args);
        var principals = services.GetRequiredService<IOperatorPrincipalService>();
        var created = await principals.CreateAsync(
            options.DisplayName,
            options.Roles,
            options.CredentialName,
            options.ExpiresAt,
            cancellationToken);

        Console.Out.WriteLine("Operator service principal created.");
        WriteCredential(created.User.Id, created.Credential);
    }

    private static async Task CreateCredentialAsync(
        IServiceProvider services,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = ParseCredentialCreateOptions(args);
        var credentials = services.GetRequiredService<IOperatorCredentialService>();
        var createdCredential = await credentials.CreateAsync(
            options.UserId,
            options.CredentialName,
            options.ExpiresAt,
            cancellationToken);

        Console.Out.WriteLine("Operator credential created.");
        WriteCredential(options.UserId, createdCredential);
    }

    private static async Task RevokeCredentialAsync(
        IServiceProvider services,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var credentialId = ParseCredentialRevokeOptions(args);
        var credentials = services.GetRequiredService<IOperatorCredentialService>();
        if (!await credentials.RevokeAsync(credentialId, cancellationToken))
        {
            throw new InvalidOperationException($"Operator credential '{credentialId:D}' was not found.");
        }

        Console.Out.WriteLine($"Operator credential revoked: {credentialId:D}");
    }

    private static void WriteCredential(Guid userId, OperatorCredentialCreationResult createdCredential)
    {
        Console.Out.WriteLine($"UserId: {userId:D}");
        Console.Out.WriteLine($"CredentialId: {createdCredential.Credential.Id:D}");
        Console.Out.WriteLine($"CredentialName: {createdCredential.Credential.Name}");
        Console.Out.WriteLine($"Token: {createdCredential.Token}");
        Console.Out.WriteLine("The token is displayed once and is not stored in plaintext.");
    }

    private static CreateOptions ParseCreateOptions(IReadOnlyList<string> args)
    {
        string? displayName = null;
        var roles = new List<string>();
        string credentialName = "default";
        DateTimeOffset? expiresAt = null;

        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            var value = RequireOptionValue(args, ref index, option);
            switch (option)
            {
                case "--display-name":
                    displayName = value;
                    break;
                case "--role":
                    roles.Add(value);
                    break;
                case "--credential-name":
                    credentialName = value;
                    break;
                case "--expires-at":
                    expiresAt = ParseTimestamp(value);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option '{option}'. {Usage}");
            }
        }

        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName)
            || displayName.Length > ApplicationUser.DisplayNameMaxLength)
        {
            throw new InvalidOperationException(
                $"--display-name is required and may not exceed {ApplicationUser.DisplayNameMaxLength} characters.");
        }

        roles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (roles.Count == 0)
        {
            throw new InvalidOperationException("At least one --role is required.");
        }

        ValidateCredentialName(credentialName);
        return new CreateOptions(displayName, roles, credentialName, expiresAt);
    }

    private static CredentialCreateOptions ParseCredentialCreateOptions(IReadOnlyList<string> args)
    {
        Guid? userId = null;
        string credentialName = "rotated";
        DateTimeOffset? expiresAt = null;

        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            var value = RequireOptionValue(args, ref index, option);
            switch (option)
            {
                case "--user-id":
                    if (!Guid.TryParse(value, out var parsedUserId) || parsedUserId == Guid.Empty)
                    {
                        throw new InvalidOperationException("--user-id must be a non-empty GUID.");
                    }
                    userId = parsedUserId;
                    break;
                case "--credential-name":
                    credentialName = value;
                    break;
                case "--expires-at":
                    expiresAt = ParseTimestamp(value);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option '{option}'. {Usage}");
            }
        }

        if (!userId.HasValue)
        {
            throw new InvalidOperationException("--user-id is required.");
        }

        ValidateCredentialName(credentialName);
        return new CredentialCreateOptions(userId.Value, credentialName, expiresAt);
    }

    private static Guid ParseCredentialRevokeOptions(IReadOnlyList<string> args)
    {
        Guid? credentialId = null;
        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            var value = RequireOptionValue(args, ref index, option);
            if (!string.Equals(option, "--credential-id", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unknown option '{option}'. {Usage}");
            }

            if (!Guid.TryParse(value, out var parsedCredentialId) || parsedCredentialId == Guid.Empty)
            {
                throw new InvalidOperationException("--credential-id must be a non-empty GUID.");
            }
            credentialId = parsedCredentialId;
        }

        return credentialId
            ?? throw new InvalidOperationException("--credential-id is required.");
    }

    private static string RequireOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        if (index + 1 >= args.Count)
        {
            throw new InvalidOperationException($"Missing value for '{option}'.");
        }
        return args[++index].Trim();
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException("--expires-at must be a valid ISO-8601 timestamp.");
        }
        return parsed;
    }

    private static void ValidateCredentialName(string credentialName)
    {
        if (string.IsNullOrWhiteSpace(credentialName))
        {
            throw new InvalidOperationException("--credential-name may not be empty.");
        }
    }

    private const string Usage =
        "Supported commands: " +
        "operator create --display-name <name> --role <role> [--role <role> ...] [--credential-name <name>] [--expires-at <ISO-8601>]; " +
        "operator credential create --user-id <guid> [--credential-name <name>] [--expires-at <ISO-8601>]; " +
        "operator credential revoke --credential-id <guid>.";

    private sealed record CreateOptions(
        string DisplayName,
        IReadOnlyList<string> Roles,
        string CredentialName,
        DateTimeOffset? ExpiresAt);

    private sealed record CredentialCreateOptions(
        Guid UserId,
        string CredentialName,
        DateTimeOffset? ExpiresAt);
}
