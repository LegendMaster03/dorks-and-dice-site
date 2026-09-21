# Tool Hosting closure boundaries

This platform registers already-running tools at runtime; registration does not deploy a
container or require a host rebuild. The registry is read on each resolution.

## Ownership and trust

The main host owns authentication, authorization, site-mode resolution, registration,
gateway routing, and shared domain contracts. Tool Management requires Dev and Trusted
Access. Owner inherits Dev through the existing claims factory. Admin and mode-scoped
Editor access alone do not qualify. Management mutations require antiforgery validation.

Register only trusted applications. Embedded JavaScript and proxied HTML run on the main
site origin. These are integration models, not browser sandboxes: a tool's JavaScript can
call other same-origin host endpoints as the signed-in user. The tool slug is a routing
and availability key, not a separate authorization principal or capability.

Tools must not mount Identity storage or another tool's private storage, receive database
credentials for them, or interpret arbitrary request headers as authenticated identity.
Separate processes, filesystem mounts, credentials, and network permissions must enforce
deployment isolation; this repository cannot prove external container isolation.

Browser Cookie and Authorization headers are stripped before proxying. Both upstream
HTTP clients disable cookie storage/replay and automatic redirects. Upstream Set-Cookie
is suppressed. The host supplies X-Forwarded-Host, X-Forwarded-Proto,
X-Forwarded-Prefix, and X-Dorks-Tool-Context-Url. Other custom headers remain untrusted.
Do not treat X-User-Id, Forwarded, or similar browser-controlled metadata as identity.

The upstream policy permits HTTP(S) without credentials/query/fragment, localhost,
loopback literals, single-label internal service names, and explicitly configured
ToolHosting:AllowedUpstreamHosts. It is a hostname policy, not DNS resolution pinning or
a port allowlist. Internal DNS and allowlisted services must be trusted. Literal
non-loopback IPs and arbitrary dotted hostnames are rejected unless allowlisted.
Traversal validation rejects dot segments, backslashes, and nested escaped traversal.

## Integration behavior

The Tool Host owns the `/tools/{slug}` mount namespace. After resolving an enabled,
mode-visible registration and applying its anonymous/authenticated access rule, the host
interprets everything following the slug according to the Tool's integration type.
This is a generic Tool Host rule; no Tool receives special route ownership by slug.

Embedded Module: the host owns and renders the normal site page shell for both the root
mount and every nested GET/HEAD route beneath it. The Tool owns the route state after its
mount point. For example, `/tools/rules-core/monsters/ancient-red-dragon` resolves the
`rules-core` registration and supplies `/monsters/ancient-red-dragon` to that module as
its current Tool route. The stable Tool base path is `/tools/{slug}` and the root Tool
route is `/`.

The host shell does not add a registration title or description above an Embedded Module's
working interface. The Tool owns its in-application heading and introductory UI; the Site
still uses the registration display name for listings and the document title.

The shell imports the configured module through `/tool-modules/{slug}/...`; that asset
namespace remains host-owned and is not interpreted as Tool route state. Relative ES
imports resolve in the module subtree. The module mounts itself into `#tool-root` and
receives `data-tool-base-path`, `data-tool-route`, and `data-tool-context-url`. The context
contract exposes the same `ToolBasePath` and `ToolRoute` values together with the existing
API base URL and identity summary.

On the initial server render, `ToolRoute` is derived only from the path beneath the Tool
mount. Query strings remain on `window.location.search`, and fragments remain on
`window.location.hash`; fragments are never sent to the server. Embedded applications may
use `history.pushState`/`history.replaceState` beneath `ToolBasePath` for client navigation
without replacing the Dorks & Dice shell, and must handle `popstate` for Back/Forward
navigation. A browser refresh or direct request of any nested Tool path returns the same
shell and restores that path as `ToolRoute`. Tool code must not navigate outside its base
path when representing internal application state.

`/tool-host/{slug}/...` remains the host API/context namespace, `/tool-modules/{slug}/...`
remains the module asset namespace, and ordinary site routes outside `/tools/{slug}` remain
site-owned. Internal Tool routes therefore do not consume or shadow Tool Host APIs or
module assets.

Import failures show a visible Tool unavailable message. GET/HEAD assets forward Accept
and selected representation headers, including Content-Encoding and Vary; they are not a
general conditional-request proxy. Upstream redirects are rejected. Module/health
requests have a three-second timeout until response headers arrive.

Embedded Module backend traffic uses
`/tool-host/{slug}/api/upstream` and `/tool-host/{slug}/api/upstream/{**proxyPath}`. Request
bodies are forwarded as streams; the Site does not JSON/base64-wrap or fully buffer them.
`ToolHosting:Proxy:MaxRequestBodyBytes` controls the transport ceiling for these upstream
routes only and defaults to 134217728 bytes (128 MiB). The per-request Kestrel body-size
feature is raised before the request body is consumed, so both known-length and streamed
or chunked bodies remain bounded. Bodies that exceed the configured Site ceiling receive
413 Payload Too Large rather than being reported as an upstream 502. Ordinary Site routes
and Proxied Application routes retain their existing server request-body limits.
Application-specific upload limits, media rules, parsing, and validation remain the Tool
backend's responsibility; raising the Site transport ceiling does not require any Tool to
accept bodies up to that size.

Proxied Application: the tool owns the response/page subtree under /tools/{slug}/.
GET/HEAD on the root without its slash receive a method-preserving 307 with the query
intact. The canonical root does not redirect. Relative links resolve beneath the slash.
POST/PUT/PATCH/DELETE/OPTIONS roots forward directly, without canonicalization. Nested
paths continue to be forwarded as proxy paths and are never rendered with the embedded
site shell.

The proxy forwards GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS, query strings, and request
bodies detected by length, Transfer-Encoding, or POST/PUT/PATCH. Exotic body-bearing
HTTP/2 GET/DELETE/OPTIONS without a length are not an advertised contract. HEAD suppresses
the response body. Safe end-to-end headers pass through; hop-by-hop headers, including
Connection-nominated fields, are removed. Bodies are not transformed. Content-Length,
Content-Type, Content-Encoding, Content-Disposition, Content-Range, Vary, validators, and
Cache-Control are preserved when provided. Conditional 304 responses pass through without
a body. Arbitrary binary response bytes are stream-copied and are not decoded as text or
transformed into JSON.

Other 3xx responses produce 502: there is no redirect following or Location rewriting.
Location on non-3xx responses passes through, so upstream applications must generate
public-prefix-safe links themselves. Root-relative links, absolute URLs, HTML, and
JavaScript are not rewritten. Cookies and tool-owned cookie sessions are unsupported.
WebSocket upgrades are unsupported.

Responses use ResponseHeadersRead and stream-copy rather than full buffering.
`ToolHosting:Proxy:RequestTimeout` controls proxy forwarding and defaults to five minutes.
The timeout covers sending the request body and waiting for upstream response headers; it
does not impose a whole-body deadline on the streamed response after headers arrive.
The incoming request cancellation token is also passed to the upstream send and response
copy, so a disconnected browser cancels the corresponding upstream work. A forwarding
timeout before headers maps to 504. There is no explicit SSE flush/heartbeat contract,
stream idle timeout, WebSocket tunnel, or recovery guarantee after response bytes are
committed. Treat SSE and indefinite responses as unsupported for this provisional proxy.
Transport failure before headers maps to 502. Mid-stream failures may abort/error the
request rather than produce a clean replacement error page.

There is no proxy cache. Upstream cache policy is passed through; deployments must avoid
shared caching of account-restricted tool responses. Previously downloaded/cacheable
assets cannot be recalled by disabling a registration. Host context and successful API
responses use no-store; membership-dependent API failures also use no-store.

## Shared contracts and native campaign context

GET /tool-host/{slug}/context can return anonymous context for an anonymous tool. Its
`ToolBasePath` is always `/tools/{slug}`. Its optional `toolRoute` query parameter is used
by the embedded shell to carry the server-resolved initial Tool route into the context
response; omitted or root route state resolves to `/`. It is route metadata, not an
authorization input and not an alternate Tool Host routing namespace.

GET /tool-host/{slug}/api/session, /api/campaigns, and /api/campaigns/{campaignId}
require host authentication and derive identity from the authenticated principal.
Browser user IDs and identity headers do not influence lookup. Disabled/wrong-mode
tools are unavailable. Session/context expose only stable user ID and display name,
not Identity entities, credentials, email, global role records, or security stamps.

Campaign access comes exclusively from the native Dorks & Dice campaign domain through
`ICampaignContextService`. Active campaign membership is required. Missing, archived, and
nonmember campaigns return the same 404 behavior. DM and Player are campaign-scoped
roles, not ASP.NET global roles. No Tool Host endpoint administers campaigns.

GET /tool-host/{slug}/api/campaigns/{campaignId}/context exposes the stable read-only
campaign projection intended for first-party Tools: campaign identity, every role held by
the requesting account, active table participants, and active campaign-linked characters.
The Tool Host does not expose Dorks & Dice EF entities or database access to Tools.

The authenticated upstream ticket is populated from the same native campaign authority.
Authentication contract version 1 represents one campaign role per entry; a native
membership holding both DM and Player is emitted as two entries for the same campaign so
existing `(campaignId, role)` authorization checks preserve both grants.

## Deployment diagnostics

Tool Management has authoritative registration metadata (enabled state, integration type,
integration contract version, configured upstream/health endpoint, and registration
timestamps) plus live transport health (HTTP result and request duration). Those values do
not identify the software build currently running in the Tool.

The current Tool contracts do not provide an authoritative application build/version
identifier or deployment timestamp. In particular:

- `IntegrationContractVersion` is the Site/Tool protocol version, not the Tool software
  version.
- `ToolRegistration.UpdatedAt` is when the Site registration changed, not when the Tool
  was built or deployed.
- a published content/rules revision belongs to Tool-owned domain data and is not an
  application build identifier.
- the health-check request time and latency say when the Site observed the service, not
  when that service was deployed.

Exposing build/deployment diagnostics therefore requires an explicit versioned contract
from each Tool runtime. At minimum, that contract must supply an opaque application build
identifier (or application version tied to a specific build) and the UTC deployment time
for the currently running build. A source revision or immutable image digest may also be
included when the Tool can supply it authoritatively. The Site should display missing
fields as unavailable rather than infer them from registration, content, container, or
health-check metadata.

The diagnostics contract may be a dedicated endpoint or a versioned extension of the
existing health contract, but its fields must be Tool/deployment supplied and must remain
distinct from the Embedded Module integration contract version.

## Runtime files

`ToolHosting:RegistryPath` supports a configurable absolute or content-root-relative path.
The default `Content/tool-registry.json` is runtime data: ignored by Git and excluded from
build and publish output. Its temporary replacement files are ignored too. Provision
persistent storage separately from application releases.

Campaigns are not Tool Host runtime files. They use the native Dorks & Dice persistence
configuration and schema owned by the Dorks & Dice mode.
