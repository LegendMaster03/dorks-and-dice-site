using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;

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
            if (args.Length < 2 || !string.Equals(args[1], "create", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Supported command: operator create --display-name <name> --role <role> [--role <role> ...] [--credential-name <name>] [--expires-at <ISO-8601>].");
            }

            var options = ParseCreateOptions(args[2..]);
            await using var scope = services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var credentials = scope.ServiceProvider.GetRequiredService<IOperatorCredentialService>();

            var unknownRoles = options.Roles
                .Where(role => !AccountRoleHierarchy.GlobalRoleNames.Contains(role, StringComparer.Ordinal))
                .ToArray();
            if (unknownRoles.Length > 0)
            {
                throw new InvalidOperationException($"Unknown global role(s): {string.Join(", ", unknownRoles)}.");
            }

            foreach (var role in options.Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                    ThrowIfFailed(createRole, $"create role '{role}'");
                }
            }

            var userId = Guid.NewGuid();
            var serviceIdentity = $"operator-{userId:N}@service.invalid";
            var user = new ApplicationUser
            {
                Id = userId,
                AccountKind = AccountKind.ServicePrincipal,
                UserName = serviceIdentity,
                Email = serviceIdentity,
                EmailConfirmed = true,
                DisplayName = options.DisplayName,
                CreatedAt = DateTimeOffset.UtcNow,
                LockoutEnabled = false
            };

            var created = await userManager.CreateAsync(user);
            ThrowIfFailed(created, "create service principal");
            try
            {
                if (options.Roles.Count > 0)
                {
                    var addRoles = await userManager.AddToRolesAsync(user, options.Roles);
                    ThrowIfFailed(addRoles, "assign service-principal roles");
                }

                var createdCredential = await credentials.CreateAsync(
                    user.Id,
                    options.CredentialName,
                    options.ExpiresAt,
                    cancellationToken);

                Console.Out.WriteLine("Operator service principal created.");
                Console.Out.WriteLine($"UserId: {user.Id:D}");
                Console.Out.WriteLine($"DisplayName: {user.DisplayName}");
                Console.Out.WriteLine($"CredentialId: {createdCredential.Credential.Id:D}");
                Console.Out.WriteLine($"Token: {createdCredential.Token}");
                Console.Out.WriteLine("The token is displayed once and is not stored in plaintext.");
            }
            catch
            {
                await userManager.DeleteAsync(user);
                throw;
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

    private static CreateOptions ParseCreateOptions(IReadOnlyList<string> args)
    {
        string? displayName = null;
        var roles = new List<string>();
        string credentialName = "default";
        DateTimeOffset? expiresAt = null;

        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            if (index + 1 >= args.Count)
            {
                throw new InvalidOperationException($"Missing value for '{option}'.");
            }

            var value = args[++index].Trim();
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
                    if (!DateTimeOffset.TryParse(value, out var parsed))
                    {
                        throw new InvalidOperationException("--expires-at must be a valid ISO-8601 timestamp.");
                    }
                    expiresAt = parsed;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option '{option}'.");
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

        if (string.IsNullOrWhiteSpace(credentialName))
        {
            throw new InvalidOperationException("--credential-name may not be empty.");
        }

        return new CreateOptions(displayName, roles, credentialName, expiresAt);
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Could not {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }

    private sealed record CreateOptions(
        string DisplayName,
        IReadOnlyList<string> Roles,
        string CredentialName,
        DateTimeOffset? ExpiresAt);
}
