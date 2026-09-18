using System.Security.Cryptography;
using System.Text;
using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using dorks_and_dice_site.Services.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Operator;

public sealed record OperatorBrowserBootstrapIssueResult(
    OperatorBrowserBootstrap Bootstrap,
    string Token);

public sealed record OperatorBrowserBootstrapConsumeResult(
    bool Succeeded,
    ApplicationUser? User,
    Guid? CredentialId,
    Guid? InvocationId);

public interface IOperatorBrowserBootstrapService
{
    Task<OperatorBrowserBootstrapIssueResult> IssueAsync(
        Guid userId,
        Guid credentialId,
        Guid issuanceInvocationId,
        CancellationToken cancellationToken = default);

    Task<OperatorBrowserBootstrapConsumeResult> ConsumeAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class OperatorBrowserBootstrapService(
    IdentityDbContext dbContext) : IOperatorBrowserBootstrapService
{
    private const string TokenPrefix = "ddboot_v1_";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public async Task<OperatorBrowserBootstrapIssueResult> IssueAsync(
        Guid userId,
        Guid credentialId,
        Guid issuanceInvocationId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userIsActive = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId
                    && user.DeletedAt == null
                    && user.AccountKind == AccountKind.ServicePrincipal,
                cancellationToken);
        if (!userIsActive)
        {
            throw new InvalidOperationException("Browser bootstrap is available only to active service principals.");
        }

        var credentialIsActive = await dbContext.OperatorCredentials
            .AsNoTracking()
            .AnyAsync(
                credential => credential.Id == credentialId
                    && credential.UserId == userId
                    && credential.RevokedAt == null
                    && (!credential.ExpiresAt.HasValue || credential.ExpiresAt > now),
                cancellationToken);
        if (!credentialIsActive)
        {
            throw new InvalidOperationException("The Operator credential is not active for this service principal.");
        }

        var id = Guid.NewGuid();
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var bootstrap = new OperatorBrowserBootstrap
        {
            Id = id,
            UserId = userId,
            CredentialId = credentialId,
            IssuanceInvocationId = issuanceInvocationId,
            SecretHash = HashSecret(secret),
            IssuedAt = now,
            ExpiresAt = now.Add(Lifetime)
        };

        dbContext.OperatorBrowserBootstraps.Add(bootstrap);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OperatorBrowserBootstrapIssueResult(
            bootstrap,
            $"{TokenPrefix}{id:N}_{secret}");
    }

    public async Task<OperatorBrowserBootstrapConsumeResult> ConsumeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseToken(token, out var bootstrapId, out var secret))
        {
            return new(false, null, null, null);
        }

        var bootstrap = await dbContext.OperatorBrowserBootstraps
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == bootstrapId, cancellationToken);
        if (bootstrap is null)
        {
            return new(false, null, null, null);
        }

        var invocationId = Guid.NewGuid();
        if (!SecretMatches(bootstrap.SecretHash, secret))
        {
            await WriteAuditAsync(bootstrap, invocationId, "Invalid", cancellationToken);
            return new(false, null, null, invocationId);
        }

        var now = DateTimeOffset.UtcNow;
        if (bootstrap.ExpiresAt <= now)
        {
            await WriteAuditAsync(bootstrap, invocationId, "Expired", cancellationToken);
            return new(false, null, null, invocationId);
        }

        if (bootstrap.ConsumedAt.HasValue)
        {
            await WriteAuditAsync(bootstrap, invocationId, "AlreadyConsumed", cancellationToken);
            return new(false, null, null, invocationId);
        }

        ApplicationUser? user = null;
        string? failedOutcome = null;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            // This conditional no-op UPDATE is deliberate. It both revalidates the credential
            // inside the consumption transaction and acquires the credential row's write lock.
            // RevokeAsync updates the same row, so concurrent revoke/consume operations are
            // serialized by the database instead of relying on the earlier in-memory check.
            var activeCredentialRows = await dbContext.OperatorCredentials
                .Where(credential => credential.Id == bootstrap.CredentialId
                    && credential.UserId == bootstrap.UserId
                    && credential.RevokedAt == null
                    && (!credential.ExpiresAt.HasValue || credential.ExpiresAt > now))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        credential => credential.LastUsedAt,
                        credential => credential.LastUsedAt),
                    cancellationToken);

            if (activeCredentialRows != 1)
            {
                failedOutcome = "CredentialUnavailable";
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                user = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate => candidate.Id == bootstrap.UserId
                            && candidate.DeletedAt == null
                            && candidate.AccountKind == AccountKind.ServicePrincipal,
                        cancellationToken);

                if (user is null)
                {
                    failedOutcome = "PrincipalUnavailable";
                    await transaction.RollbackAsync(cancellationToken);
                }
                else
                {
                    var consumedRows = await dbContext.OperatorBrowserBootstraps
                        .Where(value => value.Id == bootstrap.Id
                            && value.UserId == bootstrap.UserId
                            && value.CredentialId == bootstrap.CredentialId
                            && value.ConsumedAt == null
                            && value.ExpiresAt > now)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(value => value.ConsumedAt, now),
                            cancellationToken);

                    if (consumedRows != 1)
                    {
                        failedOutcome = "AlreadyConsumed";
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    else
                    {
                        dbContext.OperatorAuditRecords.Add(
                            BuildAudit(bootstrap, invocationId, "Succeeded", now));
                        await dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                }
            }
        }

        if (failedOutcome is not null)
        {
            await WriteAuditAsync(bootstrap, invocationId, failedOutcome, cancellationToken);
            return new(false, null, null, invocationId);
        }

        return new(true, user, bootstrap.CredentialId, invocationId);
    }

    private async Task WriteAuditAsync(
        OperatorBrowserBootstrap bootstrap,
        Guid invocationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.OperatorAuditRecords.Add(BuildAudit(bootstrap, invocationId, outcome, now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OperatorAuditRecord BuildAudit(
        OperatorBrowserBootstrap bootstrap,
        Guid invocationId,
        string outcome,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        InvocationId = invocationId,
        UserId = bootstrap.UserId,
        CredentialId = bootstrap.CredentialId,
        Client = "browser-bootstrap",
        Capability = "operator.browser_bootstrap.consume",
        Resource = $"/operator/bootstrap/{bootstrap.Id:D}",
        StartedAt = now,
        CompletedAt = now,
        Outcome = outcome
    };

    private static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static bool SecretMatches(string expectedHashText, string secret)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedHashText);
        }
        catch (FormatException)
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private static bool TryParseToken(string token, out Guid bootstrapId, out string secret)
    {
        bootstrapId = Guid.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(token)
            || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = token[TokenPrefix.Length..];
        if (payload.Length <= 33
            || payload[32] != '_'
            || !Guid.TryParseExact(payload[..32], "N", out bootstrapId))
        {
            return false;
        }

        secret = payload[33..];
        return secret.Length >= 32;
    }
}
