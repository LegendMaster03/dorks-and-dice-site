# Operator Interface

The Operator Interface lets an authorized machine identity use the Dorks & Dice Site through the same Site and Tool surfaces used by an authenticated human. It is not a parallel Tool API system.

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

Machine authentication uses separately revocable Operator bearer credentials:

```text
ddop_v1_<credential-id>_<random-secret>
```

Only the credential ID, metadata, and SHA-256 hash of the random secret are persisted. Plaintext credentials are returned only when created.

Credentials can be created, rotated, expired, and revoked independently for the same service principal. They authenticate only endpoints that explicitly select the Operator bearer scheme. They are not normal Site cookies and are never forwarded to Tools.

Provisioning and credential maintenance use ASP.NET Identity and the Operator credential service rather than raw Identity-table writes.

## Browser-session bootstrap

The machine-to-Site transition is a short-lived one-use browser bootstrap:

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
    | atomically consumes token
    | issues normal Identity application cookie
    v
normal authenticated Site session
```

Bootstrap tokens use cryptographically strong random bytes, expire after one minute, are bound to one Site user and one Operator credential, and can be consumed only once.

Bootstrap consumption revalidates and locks the bound credential row in the same database transaction that consumes the bootstrap. Credential revocation updates that same row. The database therefore serializes concurrent revoke/consume operations instead of relying on an earlier in-memory credential check.

The long-lived Operator credential is never placed in a browser URL or converted into a cookie.

## Credential-bound Site cookies

A cookie created by Operator bootstrap is a normal ASP.NET Identity application cookie with one additional claim containing the specific Operator credential ID that created the session.

Application-cookie validation preserves the normal Identity security-stamp validation path. After normal validation, only cookies carrying the Operator credential claim perform an Operator credential lookup.

The service-principal session is rejected when the bound credential:

- is revoked;
- is expired;
- no longer exists;
- no longer belongs to that Site user;
- or the Site user is no longer an active service principal.

Security-stamp principal refresh can replace the cookie principal. When that occurs, the Operator credential claim is reattached before cookie renewal so sliding expiration can not detach the session from its credential.

Human application cookies do not contain the Operator credential claim and therefore do not query Operator credential state. Their existing authentication behavior is unchanged.

Multiple credentials for one service principal remain independent. Revoking one credential invalidates only browser sessions bound to that credential.

## Tools

Tools do not implement Operator support.

A bootstrapped service principal follows the existing Tool path:

```text
authenticated Site application cookie
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

`ToolHostAuthenticationContextFactory` is the shared implementation used by the normal authenticated Tool Host proxy to project:

- Site user ID and display name;
- effective global roles;
- campaign memberships and campaign roles;
- existing Tool-specific shared projections such as Character access.

Human principals and service principals therefore use the same Tool Host path.

The Tool continues to perform its existing authorization. Rules Core, for example, remains responsible for Rules Lawyer authorization and source grants. It does not need to know whether the Site user is a human or a service principal.

No Operator bearer credential is sent to a Tool. The normal Tool proxy strips browser `Authorization` and `Cookie` headers and injects only the existing short-lived Tool Host ticket and introspection path.

There is no Tool Operator Contract, Tool Operator manifest, Tool-specific Operator capability endpoint, Operator registration metadata, or parallel Tool API.

## Operator HTTPS/JSON surface

The retained machine-facing JSON surface is intentionally small and framework-owned:

- `GET /operator/v1/me`
- `GET /operator/v1/capabilities`
- `POST /operator/v1/browser-bootstrap`
- `GET /operator/v1/openapi.json`

There is no separate Operator content-authoring API. Once bootstrapped, a machine editor uses the normal Site authoring surfaces and therefore receives the same mode-editor authorization, Development/trusted-access boundaries, input limits, and mode-preservation behavior as a human editor.

Capability discovery and OpenAPI describe only the four framework-owned Operator operations above.

## Audit

Bearer-authenticated Operator API actions are recorded by the Operator audit filter without request or response bodies.

Browser bootstrap persistence records the service-principal user ID, requesting Operator credential ID, issuance invocation ID, issuance time, expiration time, and consumption time. Attributable bootstrap-consumption attempts are also written to the Operator audit log with a separate invocation ID and outcome.

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

That runtime authenticates to the Site with an Operator credential only to request a short-lived bootstrap, then operates the normal Site with the resulting credential-bound application cookie.

The Site does not own that runtime and this branch does not create it.

## Architectural acceptance rule

A first-party Tool that implements the existing Tool Host contracts must be usable by an authorized service principal without any Operator-specific Tool changes.

Tools receive the normal trusted Site identity context. They do not branch on whether the identity originated from a human login or a service-principal browser bootstrap.


## Owner administration UI

Trusted Owner accounts can manage service principals at `/admin/agents`.

The dashboard can create a service principal, issue additional credentials, and revoke credentials. Agent creation and credential management are Owner-only; ordinary Admin accounts continue to use `/admin/accounts` for the account-management functions they are authorized to perform.

The dashboard and the server-side `operator create` command both use `IOperatorPrincipalService`, so service-principal identity creation, role validation, and initial credential issuance have one implementation. Service principals may receive the normal UI-assignable roles but can not be assigned the Owner role.

Plaintext credentials are shown only in the immediate successful create/issue response.
