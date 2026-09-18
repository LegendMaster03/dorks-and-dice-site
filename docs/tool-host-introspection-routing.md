# Tool Host internal routing

Tool backends redeem host-issued authentication tickets and call delegated upstream routes through the main Site's internal Docker/service address. Those requests use the internal service name as the HTTP `Host`, so they do not resolve to a normal public Site mode.

The SiteMode framework fallback therefore permits only two narrow Tool Host server-to-server route shapes:

```text
/tool-host/{slug}/api/introspect
/tool-host/{sourceSlug}/api/delegate/{targetSlug}/upstream
/tool-host/{sourceSlug}/api/delegate/{targetSlug}/upstream/{**proxyPath}
```

Other Tool Host browser/session routes remain unavailable through framework fallback.

The introspection endpoint remains capability protected: it requires a valid short-lived, one-time, Tool-scoped bearer ticket and returns `401 Unauthorized` when the ticket is absent or invalid.

The delegated upstream endpoint is also capability protected. It authenticates only a valid Site-issued delegation capability. It does not derive identity or Site mode from the internal request, browser cookies, or browser-supplied identity data. The capability carries the authoritative authentication snapshot created when the source Tool redeemed its normal ticket, including the original Site mode. The Site evaluates source/target registration state, the explicit source-to-target delegation allowlist, and target visibility against that captured mode before issuing a fresh target Tool ticket.

This exception keeps internal Tool authentication and approved backend delegation independent of public hostname routing without making the broader `/tool-host/**` surface available through framework fallback.
