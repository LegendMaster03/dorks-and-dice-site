# Framework and mode architecture refactor plan

## Purpose

This refactor is intended to be the major structural rebuild for the foreseeable future. It must make the repository understandable from its physical structure, remove named-mode knowledge from shared framework code, preserve the tool-hosting model, and leave durable boundaries suitable for later packaging, licensing, or sale.

See `productization-and-legacy-cleanup.md` for productization criteria and known legacy compatibility work.

Existing behavior and security boundaries remain the compatibility target until a migration stage explicitly replaces them.

## Primary acceptance criteria

1. A developer can locate the primary implementation of a mode, tool, content subsystem, identity subsystem, fallback behavior, Development/Trusted Preview feature, or deployment concern by browsing the directory tree without repository-wide search.
2. Adding a normal site mode does not require editing shared identity code, presentation switches, stylesheet switches, tool-visibility switches, or preview-target allowlists.
3. Scoped capabilities such as `Editor` are defined once and derived for registered normal modes.
4. Tools remain a first-class subsystem. Plugins may be an integration/packaging mechanism but do not replace the Tool abstraction.
5. Tool availability is expressed against registered stable mode identifiers rather than compile-time mode booleans.
6. Deployment-specific values such as domains, canonical hosts, database topology, runtime storage paths, reverse-proxy assumptions, and server configuration are not intrinsic mode definitions.
7. Route ownership remains an explicit and reviewable security boundary.
8. The test suite is reorganized around architectural boundaries while preserving meaningful behavioral/security coverage.
9. Legacy compatibility behavior is either generalized, explicitly isolated, or migrated and removed rather than silently retained in generic code.
10. The completed migration is followed by an attack-surface review and an authorized Work-mode penetration-test prompt.

## Architectural layers

### Framework

Reusable application mechanics that can eventually live independently of this deployment:

- normal mode registration and resolution contracts
- synthetic runtime mode contracts
- framework fallback behavior
- content and revision architecture
- identity capability/scoping mechanics
- routing and ownership contracts
- tool contracts and hosting abstractions
- plugin contracts if introduced
- shared presentation infrastructure
- reusable Development/Trusted Preview global administration infrastructure
- framework/module contract tests

Framework code must not assume the existence of Professional or Dorks & Dice.

### Standard modes

Professional and Dorks & Dice represent the normal site-mode shape. Future normal modes are expected to resemble these.

A standard mode defines intrinsic site identity and mode-owned behavior, including as needed:

- stable identifier and display name
- presentation metadata
- views and branding
- static assets
- mode-owned routes/features
- content policies
- tool availability contributions

Normal modes participate in the shared content system and automatically receive mode-scoped capabilities such as `Editor`. A normal mode should not need to opt into those baseline behaviors individually.

A mode must not contain production hostnames, TrueNAS/Tailscale details, production database addresses, or other deployment-specific knowledge.

### Framework fallback (`Unassigned` compatibility state)

`Unassigned` is not a peer site identity. It is the legacy runtime representation of framework fallback behavior used when mode-owned presentation or other overridable behavior is unavailable or can not be resolved.

The long-term framework should ask whether the active mode provides a component and fall back to the framework default when it does not. Fallback should not need to masquerade as a normal registered site mode.

It therefore receives no content scope and no mode-scoped editor role.

### Synthetic Development mode (`Trusted Preview` compatibility terminology)

`Development` is a framework-owned synthetic runtime mode. `Trusted Preview` remains compatibility terminology for the trusted control-plane surface that Development provides.

Development is not a peer site/tenant mode and is not registered in the normal `ISiteModeRegistry`. It has its own stable runtime identity and presentation/asset metadata, but it does not receive normal deployment host/source policy or an automatically generated `Editor @ development` role.

A Development request may also carry an `ActiveMode` that represents the normal mode currently being previewed. That preview target supplies normal-mode presentation and route context; it does not replace Development as the request's control-plane identity.

Development authorization is global and security-sensitive. `Global Editor` authority, including normal `Admin`/`Owner` inheritance, governs the editor capability in the synthetic mode. A mode-scoped Editor does not become cross-mode merely because Development is active, and the `Dev` role does not itself imply Editor authority.

Development content inspection spans normal mode assignments only after content-source composition. It has no implicit database policy: the developer database-source controls explicitly select the source set, including the valid state of selecting no databases. Normal mode source defaults and overrides must never be used as an implicit Development fallback.

### Tools

A tool is an application capability. Current hosting models remain valid concepts:

- embedded module
- proxied application

Future hosting/integration mechanisms may include in-process modules/plugins or other providers. Hosting mechanism is not the tool's identity.

Tools may be enabled for one or more registered standard modes. Optional tool-to-tool integrations must not create unnecessary hard dependencies.

### Deployment

Deployment composes framework, standard modes, synthetic runtime modes, and tools for a specific installation. It owns:

- host/domain to mode mappings and trusted development ingress
- canonical-host behavior
- enabled normal modes
- runtime content-source topology
- identity/content database configuration
- tool registry/runtime paths
- reverse proxy and trusted-network configuration
- environment-specific hosting configuration

## MediaWiki/Wikimedia reference principles

MediaWiki and Wikimedia are reference architectures rather than templates to copy verbatim.

Patterns worth evaluating and adapting:

- wiki-farm/site-specific configuration loading
- separate reusable core and production composition
- declarative extension registration
- phased startup: load -> register -> validate -> compose -> freeze -> run
- dependency declarations and optional extension integrations
- rights/capabilities separated from user groups
- independently replaceable presentation/skin modules
- page/revision/content separation

Patterns to avoid copying blindly:

- global mutable configuration
- unrestricted service-locator use
- large untyped hook surfaces that obscure control flow
- unnecessary backward-compatibility constraints
- MediaWiki permission limitations where capability + scope is clearer

## Target normal-mode registration model

The first implementation milestone is a registry/descriptor abstraction that becomes the shared source of truth for normal mode metadata.

Conceptually:

```text
SiteModeDefinition
    Id
    DisplayName
    ViewRoot
    AssetRoot
    Presentation
    RouteOwnershipContribution
```

Normal content/scoped-editor behavior is baseline behavior rather than repeated feature flags.

During migration, the current enum and `Unassigned`/`Development` values may remain as compatibility mappings. They should not determine the long-term public normal-mode contract. Development's runtime contract is instead represented explicitly as a synthetic mode outside the normal registry.

The normal registry must support synthetic/test normal modes without adding production enum values or identity configuration. Those test modes are distinct from framework-owned synthetic runtime modes such as Development.

## Scoped capability model

The framework defines a capability once:

```text
Editor
```

Registered normal modes derive scoped assignments:

```text
Editor @ dorks-and-dice
Editor @ professional
Editor @ future-mode
```

`Global Editor` inherits `Editor` for every applicable registered normal mode. `Admin` and `Owner` continue to inherit according to the account-role hierarchy.

Fallback and synthetic Development do not receive generated editor scopes. Development resolves Editor capability from global editor authority rather than inventing a synthetic scoped role.

Adding a normal mode must not require `AccountRoleScopes.<Mode>` constants or new named-mode authorization branches.

## Tool-mode composition

`ToolRegistration.Modes` already stores stable mode strings and is structurally close to the target. The refactor should remove explicit edit-model booleans and named-mode visibility switches.

The management UI should enumerate registered normal modes and bind selected stable IDs.

Legacy registrations with no mode list must be migrated or handled behind an explicit compatibility policy rather than leaving Dorks & Dice hard-coded inside generic tool runtime code.

## Security boundaries to preserve

The tool-hosting closure document remains authoritative unless deliberately superseded. In particular:

- host authentication and authorization remain host-owned
- tool registration management remains Dev + Trusted Access
- browser credentials and untrusted identity headers are not forwarded to upstream tools
- upstream validation remains explicit
- mode/tool visibility fails closed
- route ownership remains explicit
- tool-private and Identity storage remain isolated
- Development remains a trusted global control plane rather than inheriting normal mode permissions
- Development database selection remains explicit and does not fall through to a preview target's normal source policy
- normal mode-scoped Editors do not gain global editor authority in Development

Security-sensitive decisions must not become arbitrary editable metadata merely for modularity.

## Planned migration stages

### Stage 0 - Baseline, research, and legacy inventory

- inventory named-mode references and current ownership
- inventory tool-mode coupling
- inventory deployment values embedded in shared code
- inventory legacy compatibility behavior and remove any code that exists solely for the retired HTML article format
- track the remaining ConsoleVariations article-specific presentation selector separately from article-format compatibility
- classify tests by subsystem and type
- record security boundaries
- compare selected MediaWiki/Wikimedia implementations

### Stage 1 - Normal mode registry

- introduce mode definition/registry contracts
- register current normal modes through the registry
- retain enum/special runtime states only as compatibility mappings where necessary
- add synthetic normal-mode tests
- migrate stable ID/display-name/editor-scope consumers to registry lookup

Goal: a synthetic normal mode proves that identity/editor derivation works without named-mode identity changes.

### Stage 2 - Replace named-mode shared switches

Migrate shared consumers incrementally, including:

- Development preview-target enumeration
- tool visibility and management UI
- stylesheet and presentation resolution
- partial/view-root resolution
- sitemap contributions
- account navigation/editor UI
- content-source mode overrides

Route ownership remains an explicit security contract rather than becoming implicitly permissive.

### Stage 3 - Physical ownership consolidation

The repository tree must communicate ownership. Standard site modes are visibly separate from framework fallback and synthetic Development concerns.

Likely logical shape:

```text
Framework/
    Modes/
        SiteModeDefinition.cs
        SiteModeRegistry.cs
        SiteModeResolver.cs
    Fallback/
        Presentation/
        Views/
        Assets/
    TrustedPreview/
        DevelopmentRuntimeMode.cs
        ...

Modes/
    Professional/
        Presentation/
        Services/
        Views/
        Assets/
    DorksAndDice/
        Presentation/
        Services/
        Views/
        Assets/
```

Keep this flatter than necessary until subsystem size justifies additional folders.

### Stage 4 - Framework/deployment separation and productization

- move host/domain mappings to deployment composition
- isolate production-only configuration and infrastructure assumptions
- separate reusable framework registration from this installation's composition
- ensure a normal mode can be hosted under different domains without modifying the mode module
- identify supported public extension points and document them
- ensure the framework can be packaged independently without personal deployment assumptions

### Stage 5 - Tool/plugin architecture review

- preserve Tool as the primary application abstraction
- evaluate plugin/module manifests or registration APIs as optional integration mechanisms
- support optional tool-to-tool integrations without hard dependencies
- validate hosting-provider boundaries

### Stage 6 - Content architecture and legacy cleanup

Compare the existing article/revision/storage system with MediaWiki's page/revision/content separation and content handlers. Adopt only changes that solve concrete coupling or extensibility problems.

The retired HTML article format has no compatibility requirement: all legacy articles were converted to the Markdown-backed content system and the old versions were removed. Delete any renderer, parser, route, view, migration shim, test fixture, or other code found to exist solely for that retired format rather than preserving or generalizing it.

The known ConsoleVariations Free the Bees logo selector is a separate presentation holdover. It may remain temporarily while the constrained content-presentation/theme mechanism is revisited, but it must ultimately be migrated to a supported generic presentation mechanism or removed so a named article selector does not remain permanently embedded in the Professional mode stylesheet.

Do not add multi-content revisions or arbitrary per-article CSS execution solely because they are possible.

### Stage 7 - Test-suite restructuring

- use existing integration tests as characterization coverage during migration
- separate framework contracts, standard-mode tests, tool-hosting tests, synthetic Development/security tests, deployment integration tests, and test support
- parameterize repeated mode matrices where that preserves intent
- retain focused policy tests and end-to-end security wiring tests where they protect different failure modes
- remove tests only when equivalent coverage is demonstrable

### Stage 8 - Extraction/product proof

Prove that at least one real normal mode has no inappropriate dependency on another normal mode or this deployment's host configuration.

Prove a synthetic consumer can add a normal mode through the intended public contracts without editing framework internals.

Razor Class Library/project splitting remains a likely packaging technique but occurs only after logical boundaries are proven.

### Stage 9 - Security completion

- perform an architectural threat-model review against the final repository and deployment
- identify authentication, authorization, escalation, mode isolation, synthetic Development, route ownership, content/media, tool-hosting, proxy, input-validation, and configuration/secrets attack surfaces
- prepare a detailed authorized penetration-test prompt for a Work session
- remediate findings and rerun regression/security tests

## One-rebuild rule

Where a structural requirement is already known to be necessary for a clean reusable/productizable architecture, address it during this refactor rather than intentionally preserving a design that will require another foundational rebuild shortly afterward.

This is not permission to add speculative product features. Durable boundaries and extension contracts are in scope; unrelated feature expansion is not.

## First concrete implementation milestone

Before moving files, make the normal-mode registry authoritative while preserving behavior and represent Development explicitly as a synthetic runtime mode rather than a normal registry entry or a collection of unrelated preview exceptions.

Known initial consumers include:

- `SiteModeValues`
- `SiteModeMiddleware`
- `SiteModeContext`
- synthetic Development runtime metadata
- `SiteModePartialResolver`
- `SiteModeStylesheetResolver`
- presentation registration/resolution
- `SiteRouteOwnership`
- `ContentSourceRegistry`
- scoped editor-role derivation
- Development/Trusted Preview UI and validation
- sitemap generation
- tool visibility and tool-management mode selection

The registry must become authoritative before physical file movement. Otherwise the current coupling would merely be relocated into cleaner-looking folders. Synthetic Development must remain outside that normal registry while using the same runtime contracts where a control-plane mode genuinely needs mode identity.
