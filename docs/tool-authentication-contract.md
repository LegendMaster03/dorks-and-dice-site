# Tool authentication and authorization contract

The main Dorks & Dice host is the identity and campaign authority for separately running Tools. A Tool does not mount Identity storage, interpret browser-controlled identity headers, or create a parallel production account system.

## Browser/UI authorization

An authenticated Embedded Module can query the existing host API beneath its context `ApiBaseUrl`:

- `GET /tool-host/{slug}/api/session` returns the stable user summary, active site mode, and the user's effective global roles.
- `GET /tool-host/{slug}/api/campaigns` returns active native Dorks & Dice campaigns in which the current user has an explicit membership. The response retains the original Tool Host `id`, `name`, and single `role` compatibility shape; when a native membership has both roles, `DM` is the compatibility role.
- `GET /tool-host/{slug}/api/campaigns/{campaignId}` verifies membership without accepting a browser-supplied user ID and returns the compatibility campaign summary.
- `GET /tool-host/{slug}/api/campaigns/{campaignId}/context` returns the stable native campaign projection for an authorized member: the campaign ID/name, all roles held by the requesting account, active campaign members with their scoped roles, and active campaign-linked characters. It does not expose persistence entities.

The campaign context projection is the intended first-party Tool boundary for roster-aware integrations such as Block Initiative. Tools must not read the Dorks & Dice campaign database directly.

This data is suitable for rendering UI controls, but UI visibility is not the enforcement boundary.

For Rules Core, canonical Dorks & Dice mutation authority is represented by the Dorks & Dice mode-scoped `Rules Lawyer` role. It is not a cross-mode global role. Campaign mutation authority is represented separately by campaign membership/role. The Tool must enforce the appropriate rule again on its backend.

## Authenticated backend gateway

Embedded Modules send normal backend API traffic through:

```text
/tool-host/{slug}/api/upstream/{tool-backend-path}
```

The host authenticates the browser request, resolves the enabled Tool and active mode, obtains campaign memberships for the authenticated user, and creates a random one-time authentication ticket. Browser `Cookie`, `Authorization`, forwarding headers, and every reserved Tool control header are stripped before proxying.

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
- stable Tool registration key and, for applications, Tool slug;
- active site mode;
- stable user ID and display name;
- effective global roles;
- effective account roles scoped to the active site mode;
- active campaign memberships and campaign roles;
- optional Tool-specific authorization projections supplied by the Site;
- optional ordinary-delegation source provenance for a target-scoped delegated context; and
- optional private-tunnel source provenance for a target-scoped private context.

Authentication contract version 1 represents one campaign role per campaign entry. Native campaign memberships may contain both `DM` and `Player`; in that case the host emits one authentication-context entry per role so existing Tool backends can continue authorizing by `(campaignId, role)` without losing either grant.

Version 1 also permits backward-compatible additive projections. Current Site versions add `scopedRoles`, containing only roles effective for the active `siteMode`. Older Site versions omit this field. A Tool that consumes mode-scoped authority may retain a narrow legacy fallback when `scopedRoles` is absent so Site and Tool deployments can roll independently, but once the field is present it is authoritative for scoped roles.

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

Tickets expire after 30 seconds, are scoped to one Tool registration identity, and are consumed on redemption. Existing application compatibility retains the legacy slug introspection route while current tickets and headless service tickets use the stable registration key as their authoritative scope. Tickets are process-local because the site currently runs as one application instance. A future multi-instance deployment must replace the ticket store with shared ephemeral storage while preserving the contract.

## Ordinary Tool-to-Tool backend delegation

A source Tool authentication ticket can not be forwarded to another Tool. Normal tickets are one-time, Tool-scoped capabilities, so forwarding a `character-sheet` ticket to Rules Core would either fail target scope validation or weaken the isolation the ticket contract is intended to provide.

When a successfully introspected source Tool has one or more configured `DelegationTargets`, the Site returns two additive response headers alongside the unchanged version-1 JSON authentication context:

```text
X-Dorks-Tool-Delegation-Capability: <random short-lived capability>
X-Dorks-Tool-Delegation-Path: /tool-host/{sourceSlug}/api/delegate/{targetKey}/upstream
```

These headers are server-only. They are not added to browser-visible Tool context. Existing Tools may ignore them without changing their authentication implementation.

The source capability:

- is generated from 32 cryptographically random bytes and uses the `ddtd_v1_` namespace;
- expires after 30 seconds;
- is bound to the authenticated source Tool and the authoritative Site authentication snapshot;
- may be reused for at most 32 authorized downstream control-plane calls during that short window;
- is not a normal Tool ticket and can not be redeemed through a Tool introspection endpoint; and
- is never forwarded to a target Tool.

For ordinary delegation, the source backend calls the Site through:

```text
/tool-host/{sourceSlug}/api/delegate/{targetKey}/upstream/{targetPath}
```

and authenticates that server-to-server request with:

```text
Authorization: Bearer <source capability>
```

Browser cookies, browser identity headers, browser-supplied user IDs, Tool authentication headers, delegation headers, and private-tunnel headers do not establish delegated identity.

Delegation is explicitly allowlisted on the source Tool registration through `DelegationTargets`. Existing registrations default to no permitted targets. The immediate Dorks & Dice relationship is `character-sheet -> rules-core`; there is no reverse `rules-core -> character-sheet` grant. When a Character Sheet registration is created for the first time, the registry adds `rules-core` as its initial first-party delegation target. Upgrade migration also seeds that target when the delegation-target column is first introduced. These defaults apply only at initial provisioning/migration: later Development Tool edits, including deliberately removing `rules-core`, remain authoritative and are not restored on subsequent startups. The Development Tool editor is the authorized configuration surface for these ordinary delegation targets.

For a delegated request, the Site validates the source registration, the source-to-target allowlist, the target registration, target enabled state, the target's visibility in the **captured source Site mode**, the supported ordinary delegation integration contract, and the normal upstream URL policy. The internal server-to-server request's Host does not determine the Site mode.

The Site then reconstructs a target-specific `ToolHostAuthenticationContext`. It preserves the authoritative source snapshot for stable Site user ID, display name, Site mode, effective global roles, effective roles scoped to that Site mode, and campaign memberships/roles, but it rebuilds Tool-specific projections for the target. For example, Character Sheet's `characters` projection is not copied into a Rules Core context.

Ordinary delegated contexts carry:

```text
DelegatedFromToolKey
DelegatedFromToolSlug
```

Those fields identify the immediate ordinary delegation source. They do **not** grant access to a target's private API.

Finally, the Site issues a fresh normal target Tool authentication ticket and proxies the request through the existing authenticated Tool proxy. The target receives the same normal headers it would receive from a browser-originated authenticated gateway request:

```text
X-Dorks-Tool-Auth-Ticket: <fresh target-scoped one-time ticket>
X-Dorks-Tool-Auth-Introspection-Path: <target introspection path>
```

The target redeems the fresh ticket through its existing introspection endpoint and applies its normal authorization rules. For Rules Core public APIs, this preserves the same stable Site user identity so its existing per-user source grants continue to apply independently of global or campaign authority.

The delegated route reuses normal proxy streaming, redirect rejection, timeout behavior, upstream host policy, query/body forwarding, target `X-Forwarded-*` semantics, target context URL, and request-body limits.

## Private Tool tunnels

Private Tool tunnels are a separate authorization and deployment relationship. They do not reuse `DelegationTargets`, and normal Tool delegation does not imply private API access.

Private relationships are deployment/security configuration under:

```text
ToolHosting:PrivateTunnels:{sourceToolKey}:{index} = {targetToolKey}
```

The relationship is pair-specific. If another Tool needs private access to the same target, it requires its own configured source-to-target relationship.

When an authenticated source Tool has at least one configured private target, successful introspection may additionally return:

```text
X-Dorks-Tool-Private-Tunnel-Capability: <short-lived source capability>
```

The source capability proves the authoritative Site source context. It does not itself grant access to arbitrary private targets. The private ticket-exchange endpoint independently checks the configured private source/target relationship.

An application source requests a target-scoped ticket through:

```text
POST /tool-host/{sourceSlug}/api/private-tunnel/{targetKey}/ticket
```

A headless service source uses:

```text
POST /tool-host/registrations/{sourceKey}/api/private-tunnel/{targetKey}/ticket
```

The request uses:

```text
Authorization: Bearer <source capability>
```

Site validates the source registration, source enabled/visibility state, the deployment-level private source/target allowlist, and target enabled/visibility state. Private source eligibility is independent of the ordinary delegation hosting contract: registered applications and headless services can participate without becoming ordinary delegation sources.

Site then creates a fresh target-scoped `ToolHostAuthenticationContext` carrying:

```text
PrivateTunnelSourceToolKey
PrivateTunnelSourceToolSlug
```

It issues a normal target Tool authentication ticket and returns only the target key, ticket, and target introspection path. It does **not** proxy the target request and does not supply the target network URL.

The source backend obtains the target's private base URL from deployment configuration and sends the actual API request directly over the private source-to-target network path with:

```text
X-Dorks-Tool-Auth-Ticket: <fresh target-scoped one-time ticket>
X-Dorks-Tool-Auth-Introspection-Path: <target introspection path>
```

The target redeems that ticket through Site exactly as it redeems any normal target ticket. The returned trusted context contains private-tunnel provenance, which the target can require before allowing its private API.

Site is therefore the identity and authorization **control plane** for private tunnels, not the HTTP data plane. The source-to-target API body does not pass through Site.

Private network reachability and private-tunnel provenance are complementary controls. A deployment should isolate the private target ingress from the general Tool network, and the target should still reject private API requests that lack a Site-issued private-tunnel context.

For the Rules Wiki / Rules Core relationship, the intended flow is:

```text
Browser -> Rules Wiki UI
             |
             | direct private deployment path
             v
        Rules Core private API
```

Rules Wiki does not use the stable public Rules Core API for Wiki semantics, does not expose a general API of its own, and does not proxy Rules Core routes.

## Reserved Tool control headers

Browser-controlled traffic can not establish Tool identity, delegation authority, lifecycle authority, or private-tunnel authority by supplying reserved headers.

The normal Site proxy strips request and response headers in all of these namespaces:

```text
X-Dorks-Tool-Auth-*
X-Dorks-Tool-Lifecycle-*
X-Dorks-Tool-Delegation-*
X-Dorks-Tool-Private-Tunnel-*
```

Private-tunnel source capabilities remain server-only and are never forwarded to a target Tool. A private target receives only its fresh target-scoped normal authentication ticket and introspection path.

## Rules Core authorization axes

Rules Core intentionally keeps three questions independent:

1. **Is this request allowed to cross the private transport boundary?** Private Rules Core APIs require Site-issued private-tunnel provenance once the hardened boundary is activated. Public consumer APIs do not.
2. **May the authenticated user change this Rules Layer?** The site-supplied context answers canonical Dorks & Dice and campaign authority. Canonical Dorks & Dice changes require the Dorks & Dice mode-scoped `Rules Lawyer` role; campaign changes require the appropriate campaign authority.
3. **May the authenticated user access this source content?** Rules Core owns this decision through its own per-user source grants keyed by the stable Dorks & Dice user ID.

A private tunnel does not grant Rules Lawyer authority or source content grants. A Rules Lawyer does not automatically gain access to restricted source content. A user who has a source grant does not automatically gain canonical or campaign editing authority.

Storage deduplication also never broadens authorization: identical source payloads may share storage while grants remain per user.

## Standalone Tools

A Tool may provide a local development identity adapter when run independently. The Dorks & Dice ticket/introspection adapter is enabled only in the hosted environment, so standalone operation does not depend on the main site's account database.
