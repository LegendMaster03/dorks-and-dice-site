# Account linking

Dorks & Dice account linking attaches external identities to an existing authenticated Site account. It does not make an external provider a Site sign-in method.

## Architecture

The Site account remains authoritative. Linked identities are stored in the existing ASP.NET Identity external-login store (`AspNetUserLogins`), so providers do not maintain a parallel account table.

Account-link providers implement `IAccountLinkProvider`. The core supports two protocol adapters:

- `OAuthAccountLinkProvider` for ASP.NET Core OAuth handlers. Provider plugins must configure their OAuth handler to sign into `IdentityConstants.ExternalScheme`.
- `OpenIddictAccountLinkProvider` for OpenIddict Client registrations.

Provider implementations may be installed through the Site plugin system. Protocol mechanics remain in the Identity layer; provider-specific configuration remains in the provider plugin.

A link attempt carries a cryptographically random one-time nonce stored through ASP.NET Identity. The protected OAuth/OpenIddict state also carries the current Site user ID and nonce. The callback must match the authenticated Site account, provider, and unconsumed nonce before a login is attached. This application-level nonce preserves one-time linking semantics when the OpenIddict client is configured without its general-purpose token store.

OpenIddict still requires client encryption and signing credentials for interactive flows. The shared account-link client uses ephemeral encryption and signing keys because provider access and refresh tokens are never stored and an in-progress link attempt is intentionally disposable across a Site restart. Established account links remain in ASP.NET Identity and are unaffected.

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

Do not commit the Discord client secret. Direct configuration remains supported for development and deployments that use protected environment injection:

```text
AccountLinks__Discord__Enabled=true
AccountLinks__Discord__ClientId=...
AccountLinks__Discord__ClientSecret=...
```

For deployments that already use Docker/Kubernetes-style mounted secrets, prefer a secret file instead:

```text
AccountLinks__Discord__Enabled=true
AccountLinks__Discord__ClientId=...
AccountLinks__Discord__ClientSecretFile=/run/secrets/discord_client_secret
```

When both `ClientSecret` and `ClientSecretFile` are set, the direct `ClientSecret` value takes precedence.

The provider redirect URI is host-relative so the linking flow returns to the Site host that started it. Account-link controls are exposed only when the active normal mode has a matching `ModeConnections` entry for the provider. Configure the Discord OAuth application only for canonical production hosts whose modes expose Discord account linking.

In the current deployment, Discord is connected only to the `dorks-and-dice` mode, so the production callback is:

```text
https://dorks-and-dice.com/account/links/callback/discord
```

If another mode later receives a Discord connection, register that mode's canonical callback URI at the same time. A local or preview deployment that performs real Discord linking must register its own exact callback URI as well.

The initial Discord plugin requests OpenIddict's required Discord `identify` scope only. Discord role synchronization is intentionally outside this first account-linking layer.


## Global identity versus mode communities

External account identity and external community membership are deliberately separate layers.

- `AccountLinks` is global. A Site user links a Discord identity once, and that identity remains the same regardless of which normal Site mode the user is visiting.
- `ModeConnections` is mode-scoped. It identifies the external community/resource associated with a particular mode.
- Account settings expose a provider only when the active normal mode has that provider in `ModeConnections`. Connect and Disconnect actions enforce the same boundary; the underlying external identity link remains global.
- Provider infrastructure can be shared globally, but provider actions that touch an external community must first resolve the target through the mode connection.
- The same external resource may be configured for multiple modes. This is supported but is not assumed to be the normal deployment shape.

For Discord, the mode resource is a guild ID:

```json
{
  "ModeConnections": {
    "dorks-and-dice": {
      "discord": {
        "ResourceId": "1281714470799806545"
      }
    }
  }
}
```

This boundary is intended for other deep account integrations as well. For example, a globally linked GitHub identity could later interact with a GitHub organization/team selected by the active mode, without creating a second GitHub identity link for that mode.

Future Discord guild role synchronization should therefore combine:

1. the user's global Discord account link;
2. the active/target mode's Discord guild connection;
3. that mode's role-mapping policy.

It must not infer a guild globally from the Discord account-link provider.
