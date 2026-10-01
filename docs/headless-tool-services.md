# Headless Tool service registrations

The Tool registry supports two distinct kinds of separately deployed Dorks & Dice capabilities:

- **Application** — a user-facing Tool with a public slug and one of the existing UI hosting contracts (`EmbeddedModule` or `ProxiedApplication`).
- **Service** — a backend-only capability with no public Tool slug, frontend entry point, `/tools/{slug}` page, Tool card, or Tools-menu entry.

This distinction lets backend capabilities such as a headless Rules Core or shared spatial/map service participate in the Tool Host security and operational model without inventing hidden UI routes.

## Stable registration identity

Every registration has a stable `Key`. The key is the identity used for backend authorization scope, authentication tickets, ordinary Tool-to-Tool delegation allowlists, private-tunnel deployment policy, and headless service lookup.

Applications additionally have a public `Slug`. A slug is routing metadata rather than the registration's security identity.

For existing registrations, schema migration copies the existing slug into the stable key while retaining the same slug. Existing application URLs therefore do not change.

Example after migration:

```text
Kind: Application
Key: character-sheet
Slug: character-sheet
```

A headless service can instead be registered as:

```text
Kind: Service
Key: rules-core
Slug: null
Upstream: http://rules-core:8080
```

## Persistence migration

The database registry adds:

- `tool_key` — required, case-insensitively unique stable identity;
- `tool_kind` — `Application` or `Service`;
- nullable `tool_slug`;
- nullable application hosting type.

Existing rows migrate to `Application`, with their prior slug retained as both the new stable key and public slug. Descriptions, upstream URLs, health paths, modes, delegation targets, anonymous-use settings, enabled state, timestamps, and integration-contract state are preserved.

SQLite rebuilds the legacy registry table into the new constrained shape. PostgreSQL renames the old slug column to the key, adds a new nullable slug column, copies the prior route value into it, and installs the new constraints and indexes.

The legacy JSON registry reader applies the same compatibility rule in memory: when an old application record has no explicit key, its slug becomes its key.

## Public routing and navigation

Only enabled `Application` registrations with a public slug are eligible for normal Tool visibility.

A `Service` registration does not automatically receive:

- `/tools/{key}`;
- an Embedded Module shell;
- a proxied public application route;
- a Tool card on `/tools`;
- a Tools dropdown entry.

Development Tool administration lists both applications and services, including service health state and configuration.

## Authentication scope

Normal Tool authentication tickets are scoped to the stable registration key.

Application integrations keep their established introspection route:

```text
POST /tool-host/{slug}/api/introspect
```

Headless services use a key-addressed route:

```text
POST /tool-host/registrations/{key}/api/introspect
```

The authentication context includes `toolKey`. Existing application contexts retain `toolSlug` as an additive-compatible field; a service context omits the null slug.

Application v1 ticket compatibility is retained on the legacy slug endpoint. New tickets and all service tickets use the stable registration key as their authoritative scope.

## Ordinary Tool-to-Tool delegation

`DelegationTargets` is the allowlist for ordinary Tool-to-Tool delegation. It is not a private-API permission list.

The ordinary delegation flow is:

1. A browser-facing application authenticates through Site.
2. Its backend redeems a normal Tool ticket.
3. Site may issue a short-lived server-only delegation capability when the source registration has allowed targets.
4. The source backend calls the Site delegation gateway.
5. Site validates the source capability and `DelegationTargets` allowlist.
6. Site resolves the target by stable registration key.
7. Site rebuilds target-specific authorization context.
8. Site issues a fresh normal ticket scoped to the target registration key.
9. Site proxies the request to the target, which redeems the target ticket through its normal introspection endpoint.

The application delegation path is:

```text
/tool-host/{sourceSlug}/api/delegate/{targetKey}/upstream/{targetPath}
```

A headless service acting as an ordinary delegation source uses:

```text
/tool-host/registrations/{sourceKey}/api/delegate/{targetKey}/upstream/{targetPath}
```

Target authentication contexts created by this flow contain `DelegatedFromToolKey` / `DelegatedFromToolSlug` provenance. That provenance identifies the immediate ordinary delegation source. It does **not** grant access to private target APIs.

Browser Tool tickets are not delegation capabilities and can not be reused as such. Source tickets are never forwarded directly to targets.

## Private Tool tunnels

Private Tool tunnels are deliberately separate from ordinary delegation.

A private tunnel is an explicit deployment-level source-to-target relationship for a Tool that needs a target's private API. The relationship is configured under:

```text
ToolHosting:PrivateTunnels:{sourceToolKey}:{index} = {targetToolKey}
```

For example, a deployment may configure `rules-wiki -> rules-core` without changing `rules-wiki` or `rules-core` ordinary delegation targets.

The private-tunnel control-plane flow is:

1. The source Tool redeems its normal Site-issued Tool ticket.
2. If the source has at least one configured private target, Site returns a short-lived server-only private-tunnel source capability.
3. The source requests a target-scoped ticket from Site through:

   ```text
   POST /tool-host/{sourceSlug}/api/private-tunnel/{targetKey}/ticket
   ```

   or, for a headless source:

   ```text
   POST /tool-host/registrations/{sourceKey}/api/private-tunnel/{targetKey}/ticket
   ```

4. Site validates the source capability and the deployment-level private source/target allowlist.
5. Site creates a fresh target-scoped authentication context carrying `PrivateTunnelSourceToolKey` / `PrivateTunnelSourceToolSlug`.
6. Site returns the target ticket and target introspection path.
7. The source sends the actual API request **directly to the target over the deployment-provided private network path** and presents that target-scoped ticket.
8. The target redeems the ticket through normal Site introspection and authorizes its private API from the trusted private-tunnel provenance.

Site is the identity/control plane for this flow. It is not the HTTP data plane for the source-to-target API request.

The ticket exchange deliberately does not return a target base URL. Network topology is deployment-owned. A source Tool must receive its private target URL from its own deployment configuration.

Private tunnel configuration does not grant ordinary delegation. Ordinary delegation configuration does not grant private-tunnel access. Reusing the same short-lived source capability implementation does not collapse these permission models because the target-specific exchange endpoints enforce different allowlists.

If another Tool later needs a private target API, it receives its own explicitly configured private source/target relationship. Private access is never inherited transitively.

## Private network isolation

Authentication provenance and network isolation are complementary controls.

The intended deployment model is to keep a target's public consumer ingress separate from its private ingress. A private target instance or ingress should be attached only to the source/target private network plus whatever restricted control-plane network is required for Site ticket introspection. Ordinary Tools on the shared backend network must not gain direct reachability to that private ingress.

Targets should additionally enforce public-only/private-only API surface modes where supported. Network reachability alone is not private authorization, and a valid target credential does not justify exposing the private ingress on the general Tool network.

## Upstream and health behavior

Services use the same upstream URL policy, health checks, timeout behavior, request/response security filtering, and target-specific authenticated proxy path as existing hosted Tools for ordinary delegation.

Application-only forwarding metadata such as `/tools/{slug}` prefixes and browser Tool context URLs is not fabricated for headless services.

Private-tunnel data traffic does not use the Site upstream proxy and therefore is not governed by the Site proxy path. The target remains responsible for its own request handling and domain authorization.

## Development administration

Development Tool administration can create and edit service registrations using:

- stable key;
- display/admin name;
- description;
- upstream base URL;
- health path;
- site-mode availability;
- ordinary delegation targets;
- enabled state.

It does not require a service to provide:

- public slug;
- application hosting type;
- Embedded Module contract version;
- frontend entry point;
- anonymous browser-use setting.

Stable keys are immutable through the Development editor after creation.

Private tunnels are intentionally not stored in mutable Tool registration data. They are deployment/security configuration.

## Rules Core / Rules Wiki architecture

The target architecture is:

```text
Other Tools -------- stable public Rules Core API -------> Rules Core public ingress

Rules Wiki ===== private pair-specific deployment tunnel ==> Rules Core private ingress

Browser ---------------- UI traffic --------------------> Rules Wiki
```

Rules Wiki is a UI application. It must not consume Rules Core's stable public API, expose a general API of its own, or act as a transparent Rules Core API proxy.

The coordinated migration therefore needs to:

1. keep `rules-core` as the stable backend registration identity;
2. deploy a public Rules Core ingress for ordinary Tool consumers;
3. deploy a private Rules Core ingress reachable through the `rules-wiki -> rules-core` private path;
4. configure Site with that exact private source/target relationship;
5. give Rules Wiki the private Rules Core base URL through deployment configuration;
6. make Rules Wiki server-side clients exchange their Site source capability for target-scoped Rules Core tickets and call Rules Core directly;
7. remove any Rules Wiki transparent `/api/*` forwarding path; and
8. keep ordinary Tool delegation and private API authorization mechanically separate.
