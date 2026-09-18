# Operator Interface

The Operator Interface extends the Dorks & Dice Site framework so an authorized machine identity can use the same Site and Tool interfaces as an authenticated human user. It is not a parallel Tool API system.

## Identity

A machine principal is a normal Site `ApplicationUser` with `AccountKind.ServicePrincipal`.

Service principals:

- keep the normal stable Site `Guid` user ID;
- participate in the existing global-role hierarchy;
- may hold existing scoped roles and campaign memberships;
- are projected into Tool Host context exactly like human Site principals;
- can not use password or ordinary interactive login.

No password is required or created for an Operator account.

## Operator credentials

Machine authentication uses separate Operator bearer credentials. The canonical credential format is:

```text
ddop_v1_<credential-id>_<random-secret>
```

Only the credential ID, metadata, and SHA-256 hash of the random secret are persisted. Plaintext credentials are returned only when created.

Credentials can be created, replaced, expired, and revoked independently of the service-principal account. They authenticate only endpoints that explicitly select the Operator bearer scheme; they are not normal Site cookies and are never forwarded to Tools.

Provisioning and credential maintenance use ASP.NET Identity and the Operator credential service rather than raw Identity-table writes.

## Browser-session bootstrap

The central machine-to-Site transition is a short-lived browser bootstrap:

```text
machine client
    |
    | Operator bearer credential
    v
POST /operator/v1/browser-bootstrap
    |
    | validates ServicePrincipal + credential
    | persists only a hash of a fresh one-use token
    v
short-lived bootstrap URL
    |
    | browser presents bootstrap token
    v
GET /operator/bootstrap?token=...
    |
    | consumes token atomically
    | issues normal Identity application cookie
    v
normal authenticated Site session
```

Bootstrap tokens are generated from cryptographically strong random bytes, expire after one minute, are bound to one Site user and the Operator credential that requested them, and can be consumed only once. A revoked or expired credential can not consume an outstanding bootstrap.

The long-lived Operator credential is never placed in a browser URL or converted into a cookie.

The bootstrap endpoint deliberately signs the validated service principal into the normal ASP.NET Identity application-cookie scheme. `ApplicationSignInManager.CanSignInAsync` continues to reject service principals from password/interactive login; the explicit bootstrap path is separate from that prohibited login flow.

After bootstrap, the browser is an ordinary authenticated Site session. Normal Site authorization, mode/scoped-role authorization, campaign membership, and cookie validation apply.

## Tools

Tools do not implement Operator support.

For an embedded Tool, a bootstrapped service principal follows the existing path:

```text
authenticated browser cookie
        |
        v
/tools/{slug}/...
        |
        v
existing Tool Host
        |
        | existing one-use Tool Host ticket
        v
existing Tool
```

`ToolHostAuthenticationContextFactory` is the authoritative implementation for turning an authenticated Site principal into the trusted Tool Host authentication context used by the authenticated upstream proxy. Human principals and service principals therefore receive the same projection rules for:

- Site user ID and display name;
- effective global roles;
- campaign memberships and campaign roles;
- Tool-specific shared projections such as Character access where already defined.

The Tool continues to perform its existing authorization. Rules Core, for example, remains responsible for Rules Lawyer authorization and source grants. It neither knows nor needs to know whether the Site user is a human, ChatGPT, a local model, or another automation client.

No Operator bearer credential is sent to a Tool. The normal Tool proxy strips browser `Authorization` and `Cookie` headers and injects only the existing short-lived Tool Host ticket and introspection path.

There is no Tool Operator Contract, Tool Operator manifest, Operator capability endpoint inside individual Tools, or Operator-specific Tool registration metadata.

## Framework-owned structured APIs

HTTPS/JSON remains the canonical machine protocol for framework-owned facilities.

The Site-owned structured content endpoints are retained as an optional convenience because content authoring is a Site-native framework domain and the endpoints call the existing `IContentAuthoringService`, `IContentPageComposer`, `IContentAssetService`, and existing mode/editor authorization logic.

These endpoints are not required for general machine operation and do not establish a requirement that Tools expose parallel Operator APIs. A machine client can always use the normal Site interface through browser bootstrap.

Capability discovery describes only framework-owned facilities such as identity inspection, browser bootstrap, and optional Site-native content operations.

## Audit

Bearer-authenticated Operator API actions are recorded by the Operator audit filter without request or response bodies.

Browser bootstrap persistence records the service-principal user ID, requesting Operator credential ID, issuance invocation ID, issuance time, expiration time, and consumption time. Consumption attempts that can be attributed to a known bootstrap are also written to the Operator audit log with a separate invocation ID and outcome.

Secrets, browser cookies, and restricted content payloads are not logged.

After a normal Site cookie is established, existing Site and Tool/domain audit behavior remains authoritative. There is no second per-Tool Operator audit path.

## External browser runtime boundary

Chromium and Playwright do not run inside the Site process.

A future external Operator Runtime may own:

- Chromium;
- Playwright;
- DOM and accessibility inspection;
- screenshots;
- console inspection;
- network inspection.

That runtime will authenticate to the Site with an Operator credential only to request a short-lived bootstrap, then operate the normal Site with the resulting browser cookie.

The Site does not own that runtime and this branch does not create it.

## Architectural acceptance rule

A new first-party Tool that implements the existing Tool Host contracts must be usable by an authorized service principal without any Operator-specific Tool changes.

Tools receive a normal trusted Site identity context. They do not branch on whether the identity originated from a human login or a service-principal browser bootstrap.
