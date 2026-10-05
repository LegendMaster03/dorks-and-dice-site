# Tool release-audience authorization

Tool registration availability has several independent dimensions. They must not be treated as interchangeable visibility hints.

## Authoritative semantics

- `Enabled` determines whether the registration can execute at all.
- `Modes` determines the Site modes in which the registration exists. A request must be in one of the Tool's effective modes before it can execute.
- `ReleaseAudience` determines which class of user may execute an application in an allowed mode:
  - `Development` requires an authenticated user with effective global `Dev` authority.
  - `Testing` requires an authenticated user with effective mode-scoped `Tester` authority for the active Site mode. The existing account-role hierarchy makes `Dev` inherit `Tester` authority.
  - `Public` does not add a Dev/Tester role requirement.
- `AllowAnonymous` controls anonymous eligibility only after the release audience permits that user category. It never makes a `Development` or `Testing` application anonymous.

Mode membership and release audience are cumulative constraints. A Tester grant in one Site mode does not authorize a Testing Tool in another mode unless the existing role hierarchy separately grants the required role there.

## Discovery versus execution

Discovery answers whether a Tool should appear in navigation, `/tools`, public metadata, search text, or the sitemap. Execution authorization answers whether a request may open or use the Tool.

`ReleaseAudience` is an execution authorization boundary. Knowing a direct URL does not bypass it. Development and Testing applications must obey the same audience rule whether the request targets:

- `/tools/{slug}`;
- a nested `/tools/{slug}/...` route;
- `/tool-host/{slug}/context`;
- `/tool-modules/{slug}/...`;
- `/tool-host/{slug}/api/...` user-facing API/upstream routes; or
- a proxied application request using any forwarded HTTP method.

Public discovery remains narrower than execution for an authenticated user. `ToolVisibility.IsPubliclyDiscoverable(...)` is the indexing/search policy; it only exposes fully configured, enabled, Public, anonymous applications. `ToolApplicationAccessPolicy` is the authoritative user-facing execution policy.

## User-facing access policy

`ToolApplicationAccessPolicy.Evaluate(...)` is the single policy to use before a user-facing application route executes. It evaluates application kind, routability, enabled state, active mode, mode availability, release audience, authentication, and `AllowAnonymous`. Integration-contract and route-specific configuration checks may remain at the boundary that owns them, but they must not redefine release-audience semantics.

`ToolApplicationAccessMiddleware` applies that policy after authentication and before Tool controllers/proxy forwarding. This prevents a listing-only restriction: a request can not bypass release audience by bookmarking the Tool root, requesting a nested route, loading an Embedded Module directly, calling its context/API, or using a proxied application method.

For restrictive `Development`/`Testing` audience failures, user-facing application routes return `404 Not Found` so those routes do not disclose the existence of a Tool to an unauthorized caller. A Public application with `AllowAnonymous = false` retains the normal authentication challenge behavior.

Responses for Development, Testing, and account-required Public applications are forced to `Cache-Control: private, no-store`. An upstream cache directive must not make a response that depends on user authorization reusable from a shared cache.

## Embedded and proxied applications

The authorization boundary applies before Embedded Module shell rendering, module forwarding, context generation, hosted API execution, and proxied application forwarding. Each lower-level controller may retain structural checks for kind, integration type, contract support, route configuration, enabled state, and mode consistency, but those checks are not substitutes for the application access policy.

For proxied applications, authorization is evaluated before forwarding. GET, POST, DELETE, and other supported forwarded methods do not have different audience semantics. Unauthorized requests must not reach the upstream.

## Delegation and private tunnels

Backend ticket, introspection, delegation, lifecycle, and private-tunnel capability routes use their existing capability-based trust model rather than browser-route authorization. Service registrations also retain their separate service/delegation policy; application `ReleaseAudience` is not mechanically applied to services.

A delegated or private-tunnel destination that is an application is different: the initiating user's authoritative `ToolHostAuthenticationContext` carries the active Site mode plus effective global and scoped roles. `ToolApplicationAccessPolicy.CanAccessFromHostContext(...)` re-evaluates that destination application's current mode and release audience before a target application context is created. Access to a source Tool therefore does not imply access to a Development- or Testing-only destination application.

Application sources are also re-read from the current Tool registry before a delegated or private-tunnel target context is created. If a source application's release audience becomes more restrictive after a short-lived source capability was issued, that capability does not preserve the user's former execution authority: the current source mode and release audience must still admit the carried user context. Service sources continue to use their separate capability policy.

These source and destination checks prevent an accessible Tool from acting as a confused deputy and prevent a stale application capability from outliving a release-audience change. Service destinations continue to use their deliberately separate internal policy.

## Future Tool-hosting routes

Any new route that serves, bootstraps, proxies, or delegates user-facing application execution must either cross `ToolApplicationAccessMiddleware` or explicitly apply `ToolApplicationAccessPolicy` if it lives outside that route family. New discovery surfaces must use the discovery policy appropriate to their audience and must not infer execution authorization from discoverability alone.
