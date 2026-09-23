# Account linking

Dorks & Dice account linking attaches external identities to an existing authenticated Site account. It does not make an external provider a Site sign-in method.

## Architecture

The Site account remains authoritative. Linked identities are stored in the existing ASP.NET Identity external-login store (`AspNetUserLogins`), so providers do not maintain a parallel account table.

Account-link providers implement `IAccountLinkProvider`. The core supports two protocol adapters:

- `OAuthAccountLinkProvider` for ASP.NET Core OAuth handlers. Provider plugins must configure their OAuth handler to sign into `IdentityConstants.ExternalScheme`.
- `OpenIddictAccountLinkProvider` for OpenIddict Client registrations.

Provider implementations may be installed through the Site plugin system. Protocol mechanics remain in the Identity layer; provider-specific configuration remains in the provider plugin.

A link attempt carries a cryptographically random one-time nonce stored through ASP.NET Identity. The protected OAuth/OpenIddict state also carries the current Site user ID and nonce. The callback must match the authenticated Site account, provider, and unconsumed nonce before a login is attached. This application-level nonce preserves one-time linking semantics when the OpenIddict client is configured without its general-purpose token store.

## Discord

Discord is the first provider plugin and uses OpenIddict's maintained Discord web-provider integration.

Configuration:

```json
{
  "AccountLinks": {
    "Discord": {
      "Enabled": true,
      "ClientId": "...",
      "ClientSecret": "..."
    }
  }
}
```

Do not commit the Discord client secret. In deployment, prefer environment variables:

```text
AccountLinks__Discord__Enabled=true
AccountLinks__Discord__ClientId=...
AccountLinks__Discord__ClientSecret=...
```

Configure the Discord OAuth application with this redirect URI:

```text
https://dorks-and-dice.com/account/links/callback/discord
```

The initial Discord plugin requests OpenIddict's required Discord `identify` scope only. Discord role synchronization is intentionally outside this first account-linking layer.
