# Tool authentication and authorization contract

The main Dorks & Dice host is the identity and campaign authority for separately running Tools. A Tool does not mount Identity storage, interpret browser-controlled identity headers, or create a parallel production account system.

## Browser/UI authorization

An authenticated Embedded Module can query the existing host API beneath its context `ApiBaseUrl`:

- `GET /tool-host/{slug}/api/session` returns the stable user summary, active site mode, and the user's effective global roles.
- `GET /tool-host/{slug}/api/campaigns` returns active native Dorks & Dice campaigns in which the current user has an explicit membership. The response retains the original Tool Host `id`, `name`, and single `role` compatibility shape; when a native membership has both roles, `DM` is the compatibility role.
- `GET /tool-host/{slug}/api/campaigns/{campaignId}` verifies membership without accepting a browser-supplied user ID and returns the compatibility campaign summary.
- `GET /tool-host/{slug}/api/campaigns/{campaignId}/context` returns the stable native campaign projection for an authorized member: the campaign ID/name, all roles held by the requesting account, active table participants, and active campaign-linked characters. It does not expose persistence entities.

The campaign context projection is the intended first-party Tool boundary for roster-aware integrations such as Block Initiative. Tools must not read the Dorks & Dice campaign database directly.

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
- active campaign memberships and campaign roles;
- optional Tool-specific authorization projections supplied by the Site.

Authentication contract version 1 represents one campaign role per campaign entry. Native campaign memberships may contain both `DM` and `Player`; in that case the host emits one authentication-context entry per role so existing Tool backends can continue authorizing by `(campaignId, role)` without losing either grant.

Version 1 also permits backward-compatible additive Tool-specific projections. Existing Tools that do not need a projection continue receiving their prior payload shape.

### Character Sheet owner authorization projection

For the `character-sheet` Tool, the Site adds a `characters` collection to the backend authentication context. Each entry contains:

- `id`: the canonical Site `CharacterId`;
- `name`: the current Site-owned Character name;
- `status`: the current Site Character lifecycle status (`Active` or `Archived`);
- `archivedAt`: the Site archive timestamp when archived, otherwise `null`;
- `campaignIds`: the Character's current active Site campaign associations.

This projection contains only Characters canonically owned by the authenticated Site account. Archived owned Characters remain present so Character Sheet can distinguish owned-but-archived Characters from Characters that do not exist or are not owned by the account. Archiving a Character ends its active campaign associations, so an archived Character normally has an empty `campaignIds` collection unless the Site domain rules later establish another active association state.

`campaignIds` is a current authorization snapshot, not Character association history. Ended or historical associations are not included; ownership of that history remains with the Site.

Campaign authority does not broaden Character ownership. A DM does not receive another player's Character in this projection merely because that Character is associated with a campaign the DM can administer. Cross-owner DM access to detailed Character Sheet data requires a separate authorization contract.

Character Sheet may use this projection to authorize the current request, but it must not persist the projection as its authoritative Character ownership record. The Site remains authoritative for Character identity, account ownership, lifecycle, name, and campaign association.

Tickets expire after 30 seconds, are scoped to one Tool slug, and are consumed on redemption. They are process-local because the site currently runs as one application instance. A future multi-instance deployment must replace the ticket store with shared ephemeral storage while preserving the contract.

## Rules Core authorization axes

Rules Core intentionally keeps two authorization questions independent:

1. **May the authenticated user change this Rules Layer?** The site-supplied context answers global and campaign authority. Global Dorks & Dice changes require `Rules Lawyer`; campaign changes require the appropriate campaign authority.
2. **May the authenticated user access this source content?** Rules Core owns this decision through its own per-user source grants keyed by the stable Dorks & Dice user ID.

A Rules Lawyer does not automatically gain access to restricted source content. A user who has a source grant does not automatically gain global or campaign editing authority.

Storage deduplication also never broadens authorization: identical source payloads may share storage while grants remain per user.

## Standalone Tools

A Tool may provide a local development identity adapter when run independently. The Dorks & Dice ticket/introspection adapter is enabled only in the hosted environment, so standalone operation does not depend on the main site's account database.
