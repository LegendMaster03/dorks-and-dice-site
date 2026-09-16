# Operator Interface

The Operator Interface is a machine-operable frontend to the same Dorks & Dice domain services used by the human UI. It does not create a second account system, authorization hierarchy, content store, campaign store, or Tool data path.

## Authority boundaries

The Site remains authoritative for account identity, global and scoped roles, campaign membership, Tool registration, Tool Host tickets, and shared cross-Tool contracts. A machine principal is an `ApplicationUser` whose `AccountKind` is `ServicePrincipal`; it retains the normal stable Site user ID and participates in the same role and campaign systems as a human account.

Service principals have no password login path. Operator credentials are separate revocable secrets and are accepted only by endpoints that explicitly select the `OperatorBearer` authentication scheme. Ordinary account, admin, development, and MVC endpoints continue to use the normal application cookie.

Operator credentials are stored as a credential ID plus SHA-256 hash of the random secret. The plaintext token is returned only when the credential is created. Provisioning is performed through ASP.NET Identity (`UserManager` and `RoleManager`), not direct inserts into Identity tables.

## Domain-service reuse

Operator capabilities are adapters over existing services:

```text
human MVC/controller     Operator API
          |                  |
          +--------+---------+
                   |
          existing domain service
```

The initial Site content capabilities call `IContentAuthoringService`, `IContentPageComposer`, and `IContentAssetService`. Operator code must not write content persistence records directly. Mode/editor authority remains the source of truth for whether a content item may be edited.

Dorks-specific campaign and character capabilities belong under `Modes/DorksAndDice` when added. They are not part of the generic Operator framework.

## Authentication and audit

The canonical client credential format is:

```text
ddop_v1_<credential-id>_<random-secret>
```

Only the random-secret hash is persisted. Successful authentication loads the owning `ApplicationUser` and creates the principal through `IUserClaimsPrincipalFactory<ApplicationUser>`, preserving the existing global-role hierarchy, scoped roles, and claims behavior.

Every authenticated Operator action is recorded without request or response bodies. The audit record contains the invocation ID, Site user ID, credential ID, client/credential name, capability, resource path, start/completion timestamps, and outcome. Restricted source material and other response payloads are intentionally excluded.

## Tool Operator Contract v1

The Tool Operator Contract is independent of the Embedded Module integration contract. A Tool registration may opt in with an operator contract version and manifest path without changing its frontend integration type or integration-contract version.

A Tool invocation follows the existing Tool Host trust path:

```text
Operator client
    -> Site Operator API authenticates the service principal
    -> Site issues a normal one-use Tool Host authentication ticket
    -> Site proxies to the Tool's fixed Operator endpoint
    -> Tool redeems the ticket through normal Site introspection
    -> Tool receives normal Site user, role, campaign, and optional Tool-specific context
```

Tools remain responsible for their own authorization. In particular, Rules Core continues to enforce Rules Lawyer authority and its own restricted-source grants. A Rules Lawyer role does not imply access to restricted source packages.

Operator Tool Contract v1 uses:

- manifest: the path registered by the Site, normally `/operator/manifest`;
- invocation: `POST /operator/v1/invoke/{capability}` on the Tool upstream;
- capability names are opaque stable identifiers such as `rules.search`;
- the Site gateway does not expose an arbitrary upstream path or general network proxy.

Tools that do not opt into Operator Contract v1 remain unchanged.

## Protocol and adapters

HTTPS/JSON is canonical. `/operator/v1/openapi.json` describes the Site Operator surface. AI- or agent-specific protocols are adapters only; MCP, OpenAI/ChatGPT integration, local models, and future clients must call the same domain surface rather than owning business logic.

## Browser/runtime boundary

Chromium, Playwright, DOM inspection, screenshots, console/network capture, and security testing do not run inside the Site process. A future first-party Operator Runtime may provide those facilities over an internal service connection. It will not own Site or Tool databases and will use short-lived one-use browser bootstrap credentials rather than retaining a long-lived Operator credential.

The first Operator slice intentionally does not create that runtime or a general attack/proxy capability.
