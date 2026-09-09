# Tool Host introspection routing

Tool backends redeem host-issued authentication tickets by calling the main site's internal Docker address. That request uses the Docker service name as its HTTP `Host`, so it does not resolve to a normal public site mode.

The SiteMode framework fallback therefore permits exactly the Tool Host introspection capability endpoint:

```text
/tool-host/{slug}/api/introspect
```

Other Tool Host browser/session routes remain unavailable through framework fallback. The introspection endpoint itself still requires a valid short-lived bearer ticket and returns `401 Unauthorized` when a ticket is absent or invalid.

This keeps server-to-server ticket redemption independent of public hostname routing without broadening the fallback Tool Host surface.
