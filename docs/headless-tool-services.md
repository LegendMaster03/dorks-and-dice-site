# Headless Tool service registrations

The Tool registry supports two distinct kinds of separately deployed Dorks & Dice capabilities:

- **Application** — a user-facing Tool with a public slug and one of the existing UI hosting contracts (`EmbeddedModule` or `ProxiedApplication`).
- **Service** — a backend-only capability with no public Tool slug, frontend entry point, `/tools/{slug}` page, Tool card, or Tools-menu entry.

This distinction lets backend capabilities such as a future headless Rules Core or shared spatial/map service participate in the Tool Host security and operational model without inventing hidden UI routes.

## Stable registration identity

Every registration has a stable `Key`. The key is the identity used for backend authorization scope, authentication tickets, Tool-to-Tool delegation allowlists, and headless service lookup.

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

The current production `rules-core` registration is intentionally **not** converted by this architecture patch. It remains an application until the Rules Wiki frontend and headless Rules Core service can be migrated together.

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

## Tool-to-Tool delegation

Delegation allowlists contain stable target registration keys.

The security flow remains:

1. A browser-facing application authenticates through the Site.
2. Its backend redeems a normal Tool ticket.
3. The Site may issue a short-lived server-only delegation capability when the source registration has allowed targets.
4. The source backend calls the Site delegation gateway.
5. The Site validates the source capability and target allowlist.
6. The Site resolves the target by stable registration key.
7. The Site rebuilds target-specific authorization context.
8. The Site issues a fresh normal ticket scoped to the target registration key.
9. The target redeems that ticket through its normal introspection endpoint.

The legacy application delegation path remains compatible:

```text
/tool-host/{sourceSlug}/api/delegate/{targetKey}/upstream/{targetPath}
```

For compatibility, the response header sent to existing application backends continues advertising the placeholder name `{targetSlug}` even though the route segment now resolves the stable target key.

A headless service acting as a delegation source uses:

```text
/tool-host/registrations/{sourceKey}/api/delegate/{targetKey}/upstream/{targetPath}
```

Browser Tool tickets are not delegation capabilities and can not be reused as such. Source tickets are never forwarded directly to targets.

## Upstream and health behavior

Services use the same upstream URL policy, health checks, timeout behavior, request/response security filtering, and target-specific authenticated proxy path as existing hosted Tools.

Application-only forwarding metadata such as `/tools/{slug}` prefixes and browser Tool context URLs is not fabricated for headless services.

## Development administration

Development Tool administration can create and edit service registrations using:

- stable key;
- display/admin name;
- description;
- upstream base URL;
- health path;
- site-mode availability;
- delegation targets;
- enabled state.

It does not require a service to provide:

- public slug;
- application hosting type;
- Embedded Module contract version;
- frontend entry point;
- anonymous browser-use setting.

Stable keys are immutable through the Development editor after creation.

## Rules Core / Rules Wiki follow-up

This patch only provides the Site capability required for the later split. The coordinated migration still needs to:

1. create and deploy the user-facing `rules-wiki` application registration;
2. make the Rules Core deployment headless without removing APIs needed by consumers;
3. convert the existing `rules-core` registration from `Application` to `Service` only when Rules Wiki is ready to replace its current UI;
4. verify all source registrations that call Rules Core allowlist the stable `rules-core` key;
5. verify Rules Wiki uses normal Tool Host authentication and server-side delegation to Rules Core where appropriate;
6. perform the conversion without changing existing Rules Core backend identity or grants unnecessarily.
