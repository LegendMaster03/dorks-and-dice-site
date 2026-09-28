# Rules Wiki rollout compatibility

The Rules Core / Rules Wiki split changes the human-facing Tool slug from `rules-core` to `rules-wiki` while retaining `rules-core` as the stable backend service key.

## Historical browser routes

Historical browser links under `/tools/rules-core/...` must remain usable after the Rules Core registration is converted from an Application to a headless Service.

The Site therefore treats `rules-core` as a legacy browser alias only when all of the following are true:

- the request is `GET` or `HEAD`;
- no currently available public Application still owns the `rules-core` slug;
- an enabled, mode-visible `rules-wiki` Application is available.

When those conditions are met, the Site issues a method-preserving temporary redirect to `/tools/rules-wiki/...`, retaining the trailing Tool-relative path and query string.

Before the cutover, an enabled `rules-core` Application continues to win normal route resolution, so deploying this compatibility code does not move the existing UI early. If Rules Wiki is unavailable, the legacy route remains not found rather than redirecting to an unavailable Tool.

This compatibility applies only to browser routes in the `/tools` namespace. It does not rewrite `/tool-host` traffic or change the stable `rules-core` backend registration key, authentication scope, delegation target, API route, or source-grant identity.

## Registration cutover

The coordinated production cutover can therefore occur in this order:

1. Deploy Rules Wiki and register it as the `rules-wiki` Application with Embedded Module v2 and delegation target `rules-core`.
2. Confirm Rules Wiki is healthy and mode-visible.
3. Convert the existing `rules-core` registration to a headless Service while preserving its stable key and upstream backend.
4. Historical `/tools/rules-core/...` links begin redirecting to Rules Wiki automatically because no public `rules-core` Application remains.

The redirect is intentionally registration-state driven rather than controlled by a separate deployment flag.
