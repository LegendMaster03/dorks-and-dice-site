using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;

namespace dorks_and_dice_site.Services.Operator;

public sealed record OperatorPrincipalCreationResult(
    ApplicationUser User,
    OperatorCredentialCreationResult Credential);

public interface IOperatorPrincipalService
{
    Task<OperatorPrincipalCreationResult> CreateAsync(
        string displayName,
        IReadOnlyCollection<string> roles,
        string credentialName,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);
}

public sealed class OperatorPrincipalService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOperatorCredentialService credentials) : IOperatorPrincipalService
{
    public async Task<OperatorPrincipalCreationResult> CreateAsync(
        string displayName,
        IReadOnlyCollection<string> roles,
        string credentialName,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > ApplicationUser.DisplayNameMaxLength)
        {
            throw new ArgumentException(
                $"Display name must contain 1-{ApplicationUser.DisplayNameMaxLength} characters.",
                nameof(displayName));
        }

        var normalizedRoles = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedRoles.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roles));
        }

        var invalidRoles = normalizedRoles
            .Where(role => !AccountRoles.UiAssignable.Contains(role, StringComparer.Ordinal))
            .ToArray();
        if (invalidRoles.Length > 0)
        {
            throw new ArgumentException(
                $"Service principals can not be assigned these roles: {string.Join(", ", invalidRoles)}.",
                nameof(roles));
        }

        foreach (var role in normalizedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                ThrowIfFailed(roleResult, $"create role '{role}'");
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
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            LockoutEnabled = false
        };

        var createResult = await userManager.CreateAsync(user);
        ThrowIfFailed(createResult, "create service principal");

        try
        {
            var roleResult = await userManager.AddToRolesAsync(user, normalizedRoles);
            ThrowIfFailed(roleResult, "assign service-principal roles");

            var credential = await credentials.CreateAsync(
                user.Id,
                credentialName,
                expiresAt,
                cancellationToken);
            return new OperatorPrincipalCreationResult(user, credential);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }
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
}
