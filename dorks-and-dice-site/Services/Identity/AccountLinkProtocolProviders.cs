using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Client.AspNetCore;

namespace dorks_and_dice_site.Services.Identity;

public sealed class OAuthAccountLinkProvider : IAccountLinkProvider
{
    public OAuthAccountLinkProvider(AccountLinkProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Protocol != AccountLinkProtocol.OAuth)
        {
            throw new ArgumentException(
                "OAuth account-link providers must use the OAuth protocol.",
                nameof(descriptor));
        }

        Descriptor = descriptor;
    }

    public AccountLinkProviderDescriptor Descriptor { get; }

    public AuthenticationProperties CreateChallengeProperties(
        Guid userId,
        string nonce,
        string callbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackPath
        };
        properties.Items[AccountLinkAuthenticationProperties.UserId] = userId.ToString("D");
        properties.Items[AccountLinkAuthenticationProperties.Nonce] = nonce;
        return properties;
    }

    public async Task<AccountLinkAuthenticationResult?> AuthenticateAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        return CreateResult(result);
    }

    public Task CleanupAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    private AccountLinkAuthenticationResult? CreateResult(AuthenticateResult result)
    {
        if (!result.Succeeded
            || result.Principal is not ClaimsPrincipal { Identity.IsAuthenticated: true }
            || result.Properties is null)
        {
            return null;
        }

        var providerKey = AccountLinkPrincipalClaims.FindProviderKey(result.Principal);
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        return new AccountLinkAuthenticationResult(
            new AccountLinkIdentity(
                providerKey,
                AccountLinkPrincipalClaims.FindDisplayName(result.Principal)),
            result.Properties);
    }
}

public sealed class OpenIddictAccountLinkProvider : IAccountLinkProvider
{
    public OpenIddictAccountLinkProvider(AccountLinkProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Protocol != AccountLinkProtocol.OpenIddict)
        {
            throw new ArgumentException(
                "OpenIddict account-link providers must use the OpenIddict protocol.",
                nameof(descriptor));
        }

        Descriptor = descriptor;
    }

    public AccountLinkProviderDescriptor Descriptor { get; }

    public AuthenticationProperties CreateChallengeProperties(
        Guid userId,
        string nonce,
        string callbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        // OpenIddict owns the provider's configured redirection endpoint. RedirectUri is
        // the target link URI used after the external authorization demand is validated.
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/account"
        };
        properties.Items[AccountLinkAuthenticationProperties.UserId] = userId.ToString("D");
        properties.Items[AccountLinkAuthenticationProperties.Nonce] = nonce;
        return properties;
    }

    public async Task<AccountLinkAuthenticationResult?> AuthenticateAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await context.AuthenticateAsync(
            OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded
            || result.Principal is not ClaimsPrincipal { Identity.IsAuthenticated: true }
            || result.Properties is null)
        {
            return null;
        }

        var providerKey = AccountLinkPrincipalClaims.FindProviderKey(result.Principal);
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        return new AccountLinkAuthenticationResult(
            new AccountLinkIdentity(
                providerKey,
                AccountLinkPrincipalClaims.FindDisplayName(result.Principal)),
            result.Properties);
    }

    public Task CleanupAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.CompletedTask;
    }
}

internal static class AccountLinkPrincipalClaims
{
    public static string? FindProviderKey(ClaimsPrincipal principal) =>
        FirstNonBlank(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue("sub"),
            principal.FindFirstValue("id"));

    public static string? FindDisplayName(ClaimsPrincipal principal) =>
        FirstNonBlank(
            principal.FindFirstValue(ClaimTypes.Name),
            principal.FindFirstValue("name"),
            principal.FindFirstValue("preferred_username"),
            principal.FindFirstValue("username"));

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
