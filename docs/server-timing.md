# Server Timing contract for hosted Tools

The shared Site platform uses the standard HTTP `Server-Timing` response header to expose request-performance diagnostics from the Site architecture and independently deployed Tools.

This contract applies to both user-facing Tool applications and headless Tool services. The platform baseline does not require a Tool to implement `Server-Timing`: when Site directly proxies a Tool, Site already knows which Tool it dispatched to and when that upstream request produced response headers. Tool-owned instrumentation is an optional deeper layer on top of that baseline.

## Platform baseline

Site owns the `platform-*` timing namespace and emits platform observations at the response-header boundary.

For an ordinary Site response with no proxied Tool, the baseline is:

```text
Server-Timing: platform-site;dur=6.2, platform-total;dur=6.2
```

For a response directly proxied to Rules Core, Site can distinguish its own elapsed time from time spent waiting on that Tool even if Rules Core emits no timing information at all:

```text
Server-Timing: platform-site;dur=5.1, platform-tool;desc="rules-core";dur=21.3, platform-total;dur=26.4
```

The platform metrics mean:

- `platform-total` — elapsed time from the Site timing middleware's first execution until Site response headers are committed;
- `platform-tool` — Site-observed elapsed time from dispatching an HTTP request to a directly proxied Tool until that upstream request returns response headers or terminates with an upstream failure; the standard `desc` field contains the Tool's stable registration key;
- `platform-site` — `platform-total` with direct upstream Tool-wait intervals removed, representing elapsed time spent outside those upstream waits in the Site request path.

`platform-tool` is not pure Tool CPU or application execution time. It can include request-body upload, transport, connection/network latency, and Tool processing until upstream response headers arrive. It is deliberately a Site-observed boundary measurement.

If Site performs more than one direct upstream Tool request during one outer request, each observation may produce a `platform-tool` entry. Site calculates `platform-site` by removing the union of the observed upstream wait intervals, so overlapping upstream calls are not double-subtracted from the outer request time.

The Tool identity is carried in `desc` rather than being dynamically concatenated into the metric name. `desc` is part of the Server Timing standard and is exposed to browser code as `PerformanceServerTiming.description`. This preserves the stable `platform-tool` platform metric while still identifying the service Site accessed.

## Tool-owned detail

A Tool can additionally emit its own standard `Server-Timing` values. Site preserves those values when Site is the HTTP data plane and appends its own platform metrics.

With Rules Core instrumentation enabled, the same request can therefore provide both the platform observation and Rules Core's internal explanation:

```text
Server-Timing: rules-core;dur=18.7, rules-core-db;dur=12.0, platform-site;dur=5.1, platform-tool;desc="rules-core";dur=21.3, platform-total;dur=26.4
```

These values answer different questions:

- `platform-tool;desc="rules-core"` answers how long Site waited on its direct Rules Core HTTP dependency;
- `rules-core` answers how much request-to-header time Rules Core measured inside its own request pipeline;
- `rules-core-db` and other `rules-core-*` values explain portions of Rules Core's internal work;
- the difference between `platform-tool` and `rules-core` can expose transport/proxy overhead and measurement-boundary differences.

The Tool-owned component metrics can overlap each other and must not be summed blindly.

## Metric ownership and names

The namespace is ownership-based.

- `platform-*` is reserved for shared Site/platform metrics. Hosted Tools must not emit metric names beginning with `platform-`.
- A Tool owns the namespace formed from its stable Tool registration `Key`.
- The Tool's whole-request metric should use the key itself, for example `rules-core`.
- Tool submetrics should use `<tool-key>-<component>`, for example `rules-core-auth` or `rules-core-db`.
- Use the stable Tool key, not a public slug. Headless services do not necessarily have a slug, while every registration has a stable key.
- A Tool key used directly as a Tool-owned timing metric name must be valid as a `Server-Timing` metric token. Timing-capable Tool registrations should use lowercase ASCII letters, digits, and hyphens. Do not silently sanitize an incompatible registration key at emission time because different keys could collapse to the same metric namespace.
- Metric names should be stable contracts. Do not encode route parameters, user IDs, campaign IDs, entity IDs, query text, or other high-cardinality/request-specific data into metric names.

A Tool may expose only its whole-request metric initially and add useful submetrics later. A Tool may also expose no timing metrics at all; the Site baseline still distinguishes Site time from direct proxy wait time.

## Required Tool behavior

A Tool that implements deeper Server Timing should:

1. measure request duration from the Tool's earliest practical request-pipeline boundary until response headers are committed;
2. append a whole-request metric named with its stable Tool key;
3. append additional component metrics only when they measure a well-defined operation;
4. append to an existing `Server-Timing` header instead of replacing other metrics;
5. format `dur` values as milliseconds using culture-invariant numeric formatting;
6. avoid sensitive details in metric names and descriptions;
7. preserve timing output on normal error responses when response headers can still be written; and
8. guard middleware that can be re-executed so one logical request does not register duplicate whole-request callbacks.

Three decimal places are sufficient for platform diagnostics:

```text
rules-core;dur=12.347
```

Descriptions are optional for Tool-owned metrics. If used, they must remain low-cardinality and must not expose internal hostnames, SQL, credentials, tokens, paths containing private identifiers, or other sensitive infrastructure details.

## Whole-request timing semantics

Whole-request header metrics represent time to response headers, not complete response-body transmission. This is deliberate: ordinary response headers must be finalized before the body is streamed.

For most Site and Tool APIs this is a useful approximation of server work and time to first byte. A Tool that streams a long response body should not mislabel its whole-request header metric as full-body duration.

The same boundary applies to the Site platform values. `platform-total`, `platform-site`, and `platform-tool` describe the path to response headers, not the complete transfer of a large proxied response body.

## Component metrics

Tool submetrics should answer operational questions that can change a developer's next action. Good examples include:

- authentication or Site ticket introspection;
- aggregate database-command execution;
- rule resolution or projection work;
- document/materialization work;
- calls to another backend service;
- a known expensive algorithmic stage.

Do not create a metric merely because a method exists. Too many low-value metrics make the header harder to interpret and increase maintenance cost.

Component timings may overlap each other and may be nested inside the Tool's whole-request duration. Consumers must not assume that subtracting or summing arbitrary Tool-owned metrics produces another meaningful duration unless that relationship is explicitly documented by the Tool.

## Database metrics

When a Tool exposes a database metric, its semantics must be stated precisely. A provider- or framework-level timing hook may measure command execution while excluding application-side materialization, connection acquisition, domain transformation, or other persistence-adjacent work.

Use a name such as:

```text
<tool-key>-db
```

rather than exposing database product names, connection identities, table names, SQL statements, or parameters in `Server-Timing`.

The reference Rules Core implementation uses Npgsql's built-in activity source so both Entity Framework Core operations and direct Npgsql commands are represented. It requests propagation-only activity data rather than SQL/enrichment data and excludes physical connection-open spans from `rules-core-db`.

## Service-to-service and private-tunnel propagation

The Site platform baseline only observes requests for which Site is actually on the HTTP data path.

When Site directly proxies a Tool response, Site can always produce `platform-tool` and preserve any Tool-owned metrics returned by the upstream service.

Private Tool tunnels are different. Their data traffic goes directly from the source Tool to the target Tool; Site is only the identity/control plane. Site therefore can not produce a `platform-tool` observation for that private data transfer and can not infer the target Tool's internal timing.

A browser-facing Tool that calls another Tool server-side may deliberately propagate useful downstream Tool metrics to its own browser response. If it does:

- preserve the downstream Tool's metric names so ownership remains clear;
- append rather than replace the caller's own metrics;
- do not rename a downstream metric into the caller's namespace;
- do not propagate nested `platform-*` values from an internal call as though they described the outer browser request; and
- do not collapse or sum repeated downstream metrics unless the caller explicitly defines and documents that aggregate semantic.

A calling Tool may also expose its own low-cardinality dependency timing if that is operationally useful, but such a metric belongs to the calling Tool's namespace, not `platform-*`.

## Cross-origin behavior

Hosted Tool traffic normally reaches the browser through the same Site origin, so `Timing-Allow-Origin` is not required for the current hosting model.

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
rules-core-reference-materialize
rules-core-reference-total
```

Their intended meanings are:

- `rules-core` — end-to-end Rules Core request time until response headers are committed;
- `rules-core-auth` — Site Tool-ticket introspection performed by Rules Core for the request;
- `rules-core-db` — aggregate Npgsql database-operation duration for the active request before response headers are committed, excluding physical connection-open spans;
- `rules-core-reference-query` — query-stage time reported by Rules Core's reference catalog service;
- `rules-core-reference-materialize` — reference materialization time, including document loading/projection and effective-rule resolution where required;
- `rules-core-reference-total` — total reference-catalog service operation time.

The `reference-*` metrics describe work owned by the headless Rules Core service. They do not imply that Rules Core contains a Rules Wiki UI. Rules Wiki is a separate Tool that consumes Rules Core's private reference APIs through the established private Tool tunnel architecture.

A direct Rules Core response can therefore contain values similar to:

```text
Server-Timing: rules-core-auth;dur=3.8, rules-core-db;dur=12.1, rules-core-reference-query;dur=9.4, rules-core-reference-materialize;dur=2.0, rules-core-reference-total;dur=12.0, rules-core;dur=18.6
```

If Site is directly proxying that response, the outer response additionally contains the independent platform observations:

```text
Server-Timing: rules-core-auth;dur=3.8, rules-core-db;dur=12.1, rules-core;dur=18.6, platform-site;dur=5.2, platform-tool;desc="rules-core";dur=21.0, platform-total;dur=26.2
```

Exact ordering is not part of the contract.

## Implementation guidance for ASP.NET Core Tools

The Rules Core reference implementation uses this general Tool-side pattern:

- capture a monotonic timestamp at the earliest request boundary;
- register `Response.OnStarting` once per `HttpContext`;
- compute the whole-request duration inside that callback;
- append the Tool metric rather than assigning/replacing the header;
- use a per-request timing state to accumulate optional component durations; and
- let component-specific code record into that state without owning the final header lifecycle.

The Site platform independently measures its direct Tool dependency at the proxy boundary. A Tool does not need to call a Site API or duplicate Site's `platform-tool` observation.

Equivalent behavior is acceptable in non-.NET Tools. The Tool contract is the HTTP output and metric semantics, not a specific framework implementation.

## Validation checklist

For the Site platform baseline, verify that:

- an ordinary non-Tool response contains one `platform-site` and one `platform-total` metric and no `platform-tool` metric;
- a directly proxied Tool response contains `platform-site`, `platform-total`, and a `platform-tool` whose description identifies the stable Tool key even when the Tool emits no timing of its own;
- `platform-site` excludes the union of direct upstream Tool-wait intervals rather than labeling the entire outer request as Site time;
- upstream `Server-Timing` values are appended rather than replacing existing timing values; and
- middleware re-execution does not duplicate the platform timing callback.

For a Tool-owned timing implementation, verify that:

- an ordinary successful response contains the Tool's whole-request metric;
- a normal handled error response still contains the metric;
- the Tool key used for its timing namespace is token-safe and is not silently rewritten;
- component metrics use the Tool-key namespace;
- no `platform-*` metric is emitted by the Tool;
- formatting is culture invariant;
- duplicate callbacks do not create duplicate whole-request metrics; and
- tests assert metric presence and parseability without asserting fragile exact durations.

For cross-service behavior, verify that:

- a response directly proxied through Site preserves Tool-owned metrics when present while still providing the independent Site baseline;
- private-tunnel timing is not assumed to pass through Site automatically; and
- lack of Tool-owned instrumentation does not prevent Site from identifying and timing the direct Tool dependency it proxies.
