# Tool authentication and authorization contract

The main Dorks & Dice host is the identity and campaign authority for separately running Tools. A Tool does not mount Identity storage, interpret browser-controlled identity headers, or create a parallel production account system.

## Browser/UI authorization

An authenticated Embedded Module can query the existing host API beneath its context `ApiBaseUrl`:

- `GET /tool-host/{slug}/api/session` returns the stable user summary, active site mode, and the user's effective global roles.
- `GET /tool-host/{slug}/api/campaigns` returns enabled campaigns in which the current user has an explicit membership and the campaign-scoped role (`DM` or `Player`).
- `GET /tool-host/{slug}/api/campaigns/{campaignId}` verifies membership without accepting a browser-supplied user ID.

This data is suitable for rendering UI controls, but UI visibility is not the enforcement boundary.

For Rules Core, global mutation authority is represented by the Dorks-mode `Rules Lawyer` global role. Campaign mutation authority is represented separately by campaign membership/role. The Tool must enforce the appropriate rule again on its backend.

## Authenticated backend gateway

Embedded Modules send backend API traffic through:

```text
/tool-host/{slug}/api/upstream/{tool-backend-path}
```

The host authenticates the browser request, resolves the enabled Tool and active mode, obtains campaign memberships for the authenticated user, and creates a random one-time authentication ticket. Browser `Cookie`, `Authorization`, forwarding headers, and every `X-Dorks-Tool-Auth-*` header are stripped before proxying.

The host then injects only:

```text
X-Dorks-Tool-Auth-Ticket: <random one-time ticket>
X-Dorks-Tool-Auth-Introspection-Path: /tool-host/{slug}/api/introspect
```

Authenticated gateway responses are forced to `Cache-Control: no-store`.

A Tool backend must not treat either header as identity by itself. It redeems the ticket by POSTing to the supplied introspection path on the configured Dorks & Dice host with:

```text
Authorization: Bearer <ticket>
```

A successful introspection returns the authoritative `ToolHostAuthenticationContext`:

- contract version;
- Tool slug;
- active site mode;
- stable user ID and display name;
- effective global roles;
- enabled campaign memberships and campaign roles.

Tickets expire after 30 seconds, are scoped to one Tool slug, and are consumed on redemption. They are process-local because the site currently runs as one application instance. A future multi-instance deployment must replace the ticket store with shared ephemeral storage while preserving the contract.

## Rules Core authorization axes

Rules Core intentionally keeps two authorization questions independent:

1. **May the authenticated user change this Rules Layer?** The site-supplied context answers global and campaign authority. Global Dorks & Dice changes require `Rules Lawyer`; campaign changes require the appropriate campaign authority.
2. **May the authenticated user access this source content?** Rules Core owns this decision through its own per-user source grants keyed by the stable Dorks & Dice user ID.

A Rules Lawyer does not automatically gain access to restricted source content. A user who has a source grant does not automatically gain global or campaign editing authority.

Storage deduplication also never broadens authorization: identical source payloads may share storage while grants remain per user.

## Standalone Tools

A Tool may provide a local development identity adapter when run independently. The Dorks & Dice ticket/introspection adapter is enabled only in the hosted environment, so standalone operation does not depend on the main site's account database.
