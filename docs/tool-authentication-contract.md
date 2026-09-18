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

## Tool-to-Tool backend delegation

A source Tool authentication ticket can not be forwarded to another Tool. Normal tickets are one-time, Tool-scoped capabilities, so forwarding a `character-sheet` ticket to Rules Core would either fail the Tool-slug check or weaken the isolation the ticket contract is intended to provide.

When a successfully introspected source Tool has one or more configured `DelegationTargets`, the Site returns two additive response headers alongside the unchanged version-1 JSON authentication context:

```text
X-Dorks-Tool-Delegation-Capability: <random short-lived capability>
X-Dorks-Tool-Delegation-Path: /tool-host/{sourceSlug}/api/delegate/{targetSlug}/upstream
```

These headers are server-only. They are not added to browser-visible Tool context. Existing Tools may ignore them without changing their authentication implementation.

The delegation capability:

- is generated from 32 cryptographically random bytes and uses the `ddtd_v1_` namespace;
- expires after 30 seconds;
- is bound to the authenticated source Tool and the authoritative Site authentication snapshot;
- may be reused for at most 32 delegated calls during that short window, allowing one backend request to make several downstream calls;
- is not a normal Tool ticket and can not be redeemed through a Tool introspection endpoint;
- is never forwarded to a target Tool.

The source backend calls the Site through:

```text
/tool-host/{sourceSlug}/api/delegate/{targetSlug}/upstream/{targetPath}
```

and authenticates that server-to-server request with:

```text
Authorization: Bearer <delegation capability>
```

Browser cookies, browser identity headers, browser-supplied user IDs, Tool authentication headers, and delegation headers do not establish delegated identity.

Delegation is explicitly allowlisted on the source Tool registration through `DelegationTargets`. Existing registrations default to no permitted targets. The immediate Dorks & Dice relationship is `character-sheet -> rules-core`; there is no reverse `rules-core -> character-sheet` grant. When a Character Sheet registration is created for the first time, the registry adds `rules-core` as its initial first-party delegation target. Upgrade migration also seeds that target when the delegation-target column is first introduced. These defaults apply only at initial provisioning/migration: later Development Tool edits, including deliberately removing `rules-core`, remain authoritative and are not restored on subsequent startups. The Development Tool editor is the authorized configuration surface for these target slugs.

For a delegated request, the Site validates the source registration, the source-to-target allowlist, the target registration, target enabled state, the target's visibility in the **captured source Site mode**, the supported integration contract, and the normal upstream URL policy. The internal server-to-server request's Host does not determine the Site mode.

The Site then reconstructs a target-specific `ToolHostAuthenticationContext`. It preserves the authoritative source snapshot for stable Site user ID, display name, Site mode, effective global roles, and campaign memberships/roles, but it rebuilds Tool-specific projections for the target. For example, Character Sheet's `characters` projection is not copied into a Rules Core context.

Finally, the Site issues a fresh normal target Tool authentication ticket and proxies the request through the existing authenticated Tool proxy. The target receives the same normal headers it would receive from a browser-originated authenticated gateway request:

```text
X-Dorks-Tool-Auth-Ticket: <fresh target-scoped one-time ticket>
X-Dorks-Tool-Auth-Introspection-Path: /tool-host/{targetSlug}/api/introspect
```

The target therefore requires no new authentication protocol. It redeems the fresh ticket through its existing introspection endpoint and applies its normal authorization rules. For Rules Core, this preserves the same stable Site user identity so its existing per-user source grants continue to apply independently of global or campaign authority.

The delegated route reuses normal proxy streaming, redirect rejection, timeout behavior, upstream host policy, query/body forwarding, target `X-Forwarded-*` semantics, target context URL, and request-body limits. `Authorization`, `Cookie`, `X-Dorks-Tool-Auth-*`, `X-Dorks-Tool-Lifecycle-*`, and `X-Dorks-Tool-Delegation-*` are reserved and stripped before the target request. Reserved Tool authentication/delegation response headers are likewise not exposed back through the proxy.

## Rules Core authorization axes

Rules Core intentionally keeps two authorization questions independent:

1. **May the authenticated user change this Rules Layer?** The site-supplied context answers global and campaign authority. Global Dorks & Dice changes require `Rules Lawyer`; campaign changes require the appropriate campaign authority.
2. **May the authenticated user access this source content?** Rules Core owns this decision through its own per-user source grants keyed by the stable Dorks & Dice user ID.

A Rules Lawyer does not automatically gain access to restricted source content. A user who has a source grant does not automatically gain global or campaign editing authority.

Storage deduplication also never broadens authorization: identical source payloads may share storage while grants remain per user.

## Standalone Tools

A Tool may provide a local development identity adapter when run independently. The Dorks & Dice ticket/introspection adapter is enabled only in the hosted environment, so standalone operation does not depend on the main site's account database.
