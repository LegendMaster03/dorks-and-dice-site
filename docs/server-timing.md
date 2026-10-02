# Server Timing contract for hosted Tools

Dorks & Dice uses the standard HTTP `Server-Timing` response header to expose request-performance diagnostics from the Site platform and independently deployed Tools.

This contract applies to both user-facing Tool applications and headless Tool services. It is intentionally transport-oriented: the Site does not need to understand a Tool's internal implementation in order to preserve and expose its timing metrics.

## Platform behavior

The Site emits its own platform metric on responses:

```text
Server-Timing: dnd-site;dur=27.4
```

`dnd-site` measures Site processing from the Site timing middleware's first execution until response headers are committed. For a proxied Tool request this includes time spent waiting for the upstream Tool to produce its response headers. It is therefore an outer request-to-header duration, not a measure of Site CPU time alone.

When an upstream Tool returns `Server-Timing`, the Tool proxy preserves those values and the Site appends its own platform metric. A proxied response can therefore expose both layers:

```text
Server-Timing: rules-core;dur=21.8, rules-core-db;dur=11.2, dnd-site;dur=27.4
```

The metrics are not expected to add up. Nested and overlapping timings are valid and useful.

## Metric ownership and names

The namespace is ownership-based.

- `dnd-*` is reserved for Site/platform metrics. Hosted Tools must not emit metric names beginning with `dnd-`.
- A Tool owns the namespace formed from its stable Tool registration `Key`.
- The Tool's whole-request metric should use the key itself, for example `rules-core`.
- Tool submetrics should use `<tool-key>-<component>`, for example `rules-core-auth` or `rules-core-db`.
- Use the stable Tool key, not a public slug. Headless services do not necessarily have a slug, while every registration has a stable key.
- Metric names should be stable contracts. Do not encode route parameters, user IDs, campaign IDs, entity IDs, query text, or other high-cardinality/request-specific data into metric names.

A Tool may expose only its whole-request metric initially and add useful submetrics later.

## Required Tool behavior

A conforming hosted Tool should:

1. measure request duration from the Tool's earliest practical request-pipeline boundary until response headers are committed;
2. append a whole-request metric named with its stable Tool key;
3. append additional component metrics only when they measure a well-defined operation;
4. append to an existing `Server-Timing` header instead of replacing other metrics;
5. format `dur` values as milliseconds using culture-invariant numeric formatting;
6. avoid sensitive details in metric names and descriptions;
7. preserve timing output on normal error responses when response headers can still be written; and
8. guard middleware that can be re-executed so one logical request does not register duplicate whole-request callbacks.

Three decimal places are sufficient for Dorks & Dice diagnostics:

```text
rules-core;dur=12.347
```

Descriptions are optional. If used, they must remain low-cardinality and must not expose internal hostnames, SQL, credentials, tokens, paths containing private identifiers, or other sensitive infrastructure details.

## Whole-request timing semantics

The whole-request metric represents time to response headers, not complete response-body transmission. This is deliberate: ordinary response headers must be finalized before the body is streamed.

For most Dorks & Dice APIs this is a useful approximation of server work and time to first byte. A Tool that streams a long response body should not mislabel the whole-request header metric as full-body duration.

## Component metrics

Submetrics should answer operational questions that can change a developer's next action. Good examples include:

- authentication or Site ticket introspection;
- aggregate database-command execution;
- rule resolution or projection work;
- document/materialization work;
- calls to another backend service;
- a known expensive algorithmic stage.

Do not create a metric merely because a method exists. Too many low-value metrics make the header harder to interpret and increase maintenance cost.

Component timings may overlap each other and may be nested inside the Tool's whole-request duration. Consumers must not assume that subtracting or summing arbitrary metrics produces another meaningful duration unless that relationship is explicitly documented by the Tool.

## Database metrics

When a Tool exposes a database metric, its semantics must be stated precisely. For example, an Entity Framework Core interceptor can report aggregate database command-execution duration for the current HTTP request. That is not necessarily identical to all time attributable to data access, because application-side materialization or later streaming of rows can occur outside the command execution event.

Use a name such as:

```text
<tool-key>-db
```

rather than exposing database product names, connection identities, table names, or SQL statements in `Server-Timing`.

## Cross-origin behavior

Hosted Tool traffic normally reaches the browser through the same Dorks & Dice Site origin, so `Timing-Allow-Origin` is not required for the current hosting model.

Do not add broad cross-origin timing exposure merely to satisfy this contract. If a future architecture intentionally exposes Tool timing to browser code across origins, treat `Timing-Allow-Origin` as a separate security and deployment decision.

## Reference implementation: Rules Core

Rules Core is the first high-value reference implementation because it is a headless service with substantial database, authentication, rule-resolution, and reference-catalog work.

Its timing namespace is `rules-core` because `rules-core` is its stable Tool key.

The reference implementation exposes:

```text
rules-core
rules-core-auth
rules-core-db
rules-core-reference-query
rules-core-reference-docs
rules-core-reference-total
```

Their intended meanings are:

- `rules-core` — end-to-end Rules Core request time until response headers are committed;
- `rules-core-auth` — Site Tool-ticket introspection performed by Rules Core for the request;
- `rules-core-db` — aggregate Entity Framework Core database command-execution duration for the request;
- `rules-core-reference-query` — query-stage time reported by Rules Core's reference catalog service;
- `rules-core-reference-docs` — mechanical-document/materialization stage time reported by the reference catalog service;
- `rules-core-reference-total` — total reference-catalog service operation time.

The `reference-*` metrics describe work owned by the headless Rules Core service. They do not imply that Rules Core contains a Rules Wiki UI. Rules Wiki is a separate Tool that consumes Rules Core's private reference APIs through the established private Tool tunnel architecture.

A proxied or delegated request may therefore produce a header conceptually similar to:

```text
Server-Timing: rules-core-auth;dur=3.8, rules-core-db;dur=12.1, rules-core-reference-query;dur=9.4, rules-core-reference-docs;dur=2.0, rules-core-reference-total;dur=12.0, rules-core;dur=18.6, dnd-site;dur=24.3
```

Exact ordering is not part of the contract.

## Implementation guidance for ASP.NET Core Tools

The Site and Rules Core implementations use the same general pattern:

- capture a monotonic timestamp at the earliest request boundary;
- register `Response.OnStarting` once per `HttpContext`;
- compute the whole-request duration inside that callback;
- append the Tool metric rather than assigning/replacing the header;
- use a per-request timing state to accumulate optional component durations; and
- let component-specific code record into that state without owning the final header lifecycle.

This keeps header emission centralized while allowing database interceptors, authentication middleware, and domain services to contribute useful measurements.

Equivalent behavior is acceptable in non-.NET Tools. The contract is the HTTP output and metric semantics, not a specific framework implementation.

## Validation checklist

Before a Tool timing implementation is considered complete, verify that:

- an ordinary successful response contains the Tool's whole-request metric;
- a normal handled error response still contains the metric;
- existing timing values are preserved when another layer already added them;
- a hosted response through Site contains both the Tool metric and `dnd-site`;
- component metrics use the Tool-key namespace;
- no `dnd-*` metric is emitted by the Tool;
- formatting is culture invariant;
- duplicate callbacks do not create duplicate whole-request metrics; and
- tests assert metric presence and parseability without asserting fragile exact durations.
