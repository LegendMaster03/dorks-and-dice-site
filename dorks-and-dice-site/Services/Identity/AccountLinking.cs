using Microsoft.AspNetCore.Authentication;

namespace dorks_and_dice_site.Services.Identity;

public enum AccountLinkProtocol
{
    OAuth,
    OpenIddict
}

public sealed record AccountLinkProviderDescriptor(
    string Id,
    string DisplayName,
    string AuthenticationScheme,
    AccountLinkProtocol Protocol);

public sealed record AccountLinkIdentity(
    string ProviderKey,
    string? DisplayName);

public sealed record AccountLinkAuthenticationResult(
    AccountLinkIdentity Identity,
    AuthenticationProperties Properties);

public interface IAccountLinkProvider
{
    AccountLinkProviderDescriptor Descriptor { get; }

    AuthenticationProperties CreateChallengeProperties(
        Guid userId,
        string nonce,
        string callbackPath);

    Task<AccountLinkAuthenticationResult?> AuthenticateAsync(HttpContext context);

    Task CleanupAsync(HttpContext context);
}

public interface IAccountLinkProviderCatalog
{
    IReadOnlyList<IAccountLinkProvider> All { get; }

    bool TryGet(string providerId, out IAccountLinkProvider provider);
}

public sealed class AccountLinkProviderCatalog : IAccountLinkProviderCatalog
{
    private readonly IReadOnlyDictionary<string, IAccountLinkProvider> _byId;

    public AccountLinkProviderCatalog(IEnumerable<IAccountLinkProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var all = providers.ToList();
        var byId = new Dictionary<string, IAccountLinkProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in all)
        {
            ArgumentNullException.ThrowIfNull(provider);
            var descriptor = provider.Descriptor
                ?? throw new InvalidOperationException("An account-link provider returned no descriptor.");

            ValidateDescriptor(descriptor);

            if (!byId.TryAdd(descriptor.Id, provider))
            {
                throw new InvalidOperationException(
                    $"Duplicate account-link provider ID '{descriptor.Id}'.");
            }
        }

        All = all.AsReadOnly();
        _byId = byId;
    }

    public IReadOnlyList<IAccountLinkProvider> All { get; }

    public bool TryGet(string providerId, out IAccountLinkProvider provider) =>
        _byId.TryGetValue(providerId, out provider!);

    private static void ValidateDescriptor(AccountLinkProviderDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Id))
        {
            throw new InvalidOperationException("Account-link provider IDs can not be blank.");
        }

        if (!string.Equals(descriptor.Id, descriptor.Id.ToLowerInvariant(), StringComparison.Ordinal)
            || descriptor.Id.Any(character =>
                !(character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-'))
            || descriptor.Id[0] == '-'
            || descriptor.Id[^1] == '-')
        {
            throw new InvalidOperationException(
                $"Account-link provider ID '{descriptor.Id}' must use lowercase letters, numbers, and internal hyphens only.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
        {
            throw new InvalidOperationException(
                $"Account-link provider '{descriptor.Id}' has no display name.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.AuthenticationScheme))
        {
            throw new InvalidOperationException(
                $"Account-link provider '{descriptor.Id}' has no authentication scheme.");
        }
    }
}

public static class AccountLinkAuthenticationProperties
{
    public const string ProviderId = "dorks-and-dice.account-link.provider";
    public const string UserId = "dorks-and-dice.account-link.user";
    public const string ModeId = "dorks-and-dice.account-link.mode";
    public const string Nonce = "dorks-and-dice.account-link.nonce";
}

public static class AccountLinkTokenNames
{
    public const string LoginProvider = "DorksAndDice.AccountLinks";
    public const string ExternalDisplayName = "external-display-name";

    public static string Nonce(string providerId) => $"nonce:{providerId}";
}
