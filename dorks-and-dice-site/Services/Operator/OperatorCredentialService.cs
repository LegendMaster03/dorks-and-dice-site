using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Framework.Operator;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Operator;

public sealed record OperatorCredentialCreationResult(
    OperatorCredential Credential,
    string Token);

public sealed record OperatorCredentialAuthenticationResult(
    ApplicationUser User,
    OperatorCredential Credential);

public interface IOperatorCredentialService
{
    Task<OperatorCredentialCreationResult> CreateAsync(
        Guid userId,
        string name,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    Task<OperatorCredentialAuthenticationResult?> AuthenticateAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<bool> IsActiveForUserAsync(
        Guid credentialId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default);
}

public sealed class OperatorCredentialService : IOperatorCredentialService
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public OperatorCredentialService(
        IdentityDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<OperatorCredentialCreationResult> CreateAsync(
        Guid userId,
        string name,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > OperatorCredential.NameMaxLength)
        {
            throw new ArgumentException(
                $"Credential name must contain 1-{OperatorCredential.NameMaxLength} characters.",
                nameof(name));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null
            || user.DeletedAt is not null
            || user.AccountKind != AccountKind.ServicePrincipal)
        {
            throw new InvalidOperationException("Operator credentials may only be created for active service principals.");
        }

        var now = DateTimeOffset.UtcNow;
        if (expiresAt.HasValue && expiresAt.Value <= now)
        {
            throw new ArgumentException("Credential expiry must be in the future.", nameof(expiresAt));
        }

        var id = Guid.NewGuid();
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var prefix = $"{OperatorAuthenticationDefaults.TokenPrefix}{id:N}_";
        var credential = new OperatorCredential
        {
            Id = id,
            UserId = user.Id,
            Name = name,
            SecretHash = secretHash,
            SecretPrefix = prefix,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        _dbContext.OperatorCredentials.Add(credential);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new OperatorCredentialCreationResult(credential, prefix + secret);
    }

    public async Task<OperatorCredentialAuthenticationResult?> AuthenticateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseToken(token, out var credentialId, out var secret))
        {
            return null;
        }

        var credential = await _dbContext.OperatorCredentials
            .SingleOrDefaultAsync(value => value.Id == credentialId, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (credential.RevokedAt.HasValue
            || credential.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return null;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(credential.SecretHash);
        }
        catch (FormatException)
        {
            return null;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(credential.UserId.ToString());
        if (user is null
            || user.DeletedAt is not null
            || user.AccountKind != AccountKind.ServicePrincipal)
        {
            return null;
        }

        credential.LastUsedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new OperatorCredentialAuthenticationResult(user, credential);
    }

    public Task<bool> IsActiveForUserAsync(
        Guid credentialId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.OperatorCredentials
            .AsNoTracking()
            .AnyAsync(
                credential => credential.Id == credentialId
                    && credential.UserId == userId
                    && credential.RevokedAt == null
                    && (!credential.ExpiresAt.HasValue || credential.ExpiresAt > now)
                    && _dbContext.Users.Any(user =>
                        user.Id == userId
                        && user.DeletedAt == null
                        && user.AccountKind == AccountKind.ServicePrincipal),
                cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await _dbContext.OperatorCredentials
            .Where(credential => credential.Id == credentialId && credential.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(credential => credential.RevokedAt, now),
                cancellationToken);
        if (updated == 1)
        {
            return true;
        }

        return await _dbContext.OperatorCredentials
            .AsNoTracking()
            .AnyAsync(credential => credential.Id == credentialId, cancellationToken);
    }

    private static bool TryParseToken(
        string token,
        out Guid credentialId,
        out string secret)
    {
        credentialId = Guid.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(token)
            || !token.StartsWith(OperatorAuthenticationDefaults.TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = token[OperatorAuthenticationDefaults.TokenPrefix.Length..];
        if (payload.Length <= 33
            || payload[32] != '_'
            || !Guid.TryParseExact(payload[..32], "N", out credentialId))
        {
            return false;
        }

        secret = payload[33..];
        return secret.Length >= 32;
    }
}
