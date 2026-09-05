# Framework and mode architecture refactor plan

## Purpose

This refactor makes the repository understandable from its physical structure, removes mode-specific knowledge from shared framework code, preserves the current tool-hosting model, and establishes a path for publishing the reusable architecture separately from this deployment.

The refactor is intentionally staged. Existing behavior and security boundaries remain the compatibility target until a stage explicitly replaces them.

## Primary acceptance criteria

1. A developer can locate the primary implementation of a mode, tool, content subsystem, identity subsystem, or deployment concern by browsing the directory tree without repository-wide search.
2. Adding a normal site mode does not require editing shared identity code, presentation switches, stylesheet switches, tool-visibility switches, or preview-mode allowlists.
3. Scoped capabilities such as `Editor` are defined once. Mode-specific editor assignments are derived from registered normal modes.
4. Tools remain a first-class subsystem. Plugins may become an integration/packaging option for tools, but plugins do not replace the tool abstraction.
5. Tool availability is expressed against registered mode identifiers rather than compile-time mode booleans.
6. Deployment-specific values such as production domains, canonical hosts, database topology, runtime storage paths, reverse-proxy assumptions, and server configuration do not become intrinsic mode definitions.
7. Route ownership remains an explicit and reviewable security boundary.
8. The test suite is reorganized around architectural boundaries and preserves meaningful behavioral/security coverage while removing demonstrable duplication.
9. The completed migration is followed by an attack-surface review and an authorized Work-mode penetration-test prompt.

## Architectural layers

### Framework

Reusable application mechanics that can eventually live in a public framework repository:

- mode registration and resolution contracts
- fallback behavior for unavailable mode-owned presentation/functionality
- content and revision architecture
- identity capability/scoping mechanics
- routing and ownership contracts
- tool contracts and hosting abstractions
- plugin contracts if introduced
- shared presentation infrastructure
- Trusted Preview/global administration infrastructure where reusable
- test contracts for framework modules

Framework code must not assume the existence of `Professional` or `DorksAndDice`.

### Standard modes

Professional and Dorks & Dice represent the normal site-mode shape. Future modes are expected to look much more like these than like the special framework states.

A standard mode defines intrinsic site identity and mode-owned behavior. A standard mode may provide:

- stable identifier
- display name
- presentation metadata
- views and branding
- static assets
- mode-owned routes/features
- content participation/policies
- tool availability defaults or contributions where appropriate

Normal modes participate in the shared content system and automatically receive mode-scoped capabilities such as `Editor`. A normal mode should not need to opt into these baseline behaviors individually.

A mode must not contain production hostnames, TrueNAS details, Tailscale details, production database addresses, or other environment-specific deployment knowledge.

### Framework fallback (`Unassigned`)

`Unassigned` is not a peer site identity. It is the framework fallback used when a mode-owned presentation component or other overridable behavior is unavailable or can not be resolved.

Its purpose is graceful degradation and safe fallback for the rest of the system. It does not receive its own content scope or mode-scoped editor role.

### Trusted Preview (`Development` legacy runtime mode)

`Development` is the existing runtime name for the global administrative/development surface referred to as Trusted Preview.

Trusted Preview is not a peer site/tenant mode. It is a control-plane feature for the composed system that can inspect, administer, and preview normal modes and shared resources under trusted-access rules.

It therefore does not receive a normal content scope or automatically generated mode-scoped editor role. Its authorization is global and security-sensitive.

### Tools

A tool is an application capability. The current merged hosting models remain valid concepts:

- embedded module
- proxied application

Future integration mechanisms may include in-process modules/plugins or other hosting providers. Hosting mechanism is not the tool's identity.

Tools may be enabled for one or more registered standard modes. A tool may optionally integrate with another tool without making that other tool a required dependency.

### Deployment

Deployment composes framework, standard modes, and tools for a specific installation. It owns:

- domain/host to mode mappings
- canonical-host behavior
- enabled modes
- runtime content source topology
- identity/content database configuration
- tool registry/runtime paths
- reverse proxy and trusted-network configuration
- environment-specific hosting configuration

## MediaWiki/Wikimedia reference principles

MediaWiki and Wikimedia are reference architectures, not templates to copy verbatim.

Patterns worth evaluating and adapting:

- explicit wiki-farm/site-specific configuration loading
- separate reusable core and production composition
- declarative extension registration
- phased startup: load -> register -> validate -> compose -> freeze -> run
- dependency declarations and optional extension integrations
- separation between rights/capabilities and user groups
- independently replaceable presentation/skin modules
- page/revision/content separation

Patterns to avoid copying blindly:

- global mutable configuration
- unrestricted global service-locator use
- a large untyped hook surface that obscures control flow
- backward-compatibility constraints that are unnecessary for this project
- MediaWiki permission limitations where a capability-plus-scope model is clearer

## Target mode-registration model

The first implementation milestone is a registry/descriptor abstraction that becomes the shared source of truth for normal mode metadata while explicitly distinguishing framework/control-plane states.

Conceptually:

```text
SiteModeDefinition
    Id
    DisplayName
    Kind
        Standard
        Fallback
        TrustedPreview
    ViewRoot
    AssetRoot
    Presentation
    RouteOwnershipContribution
```

For a `Standard` mode, content participation and scoped capabilities are baseline behavior rather than repeated per-mode flags.

The registry must support synthetic/test standard modes so extensibility can be tested without adding another production mode or editing identity code.

## Scoped capability model

The framework defines a capability once:

```text
Editor
```

Registered standard modes generate scoped assignments:

```text
Editor @ dorks-and-dice
Editor @ professional
Editor @ future-mode
```

`Global Editor` inherits `Editor` for every applicable registered standard mode. `Admin` and `Owner` continue to inherit according to the account-role hierarchy.

`Unassigned` and Trusted Preview do not receive generated editor scopes because they are framework/control-plane states rather than normal content sites.

Adding a normal mode must not require a new `AccountRoleScopes.<Mode>` constant or a new identity authorization branch.

## Tool-mode composition

Current `ToolRegistration.Modes` is already structurally close to the target because it stores stable mode strings. The refactor should remove explicit edit-model booleans and visibility switches for named modes.

The management UI should enumerate registered standard modes and bind selected stable IDs.

Legacy behavior for registrations with no mode list must be migrated or isolated behind an explicit compatibility policy rather than silently hard-coding Dorks & Dice in the generic tool runtime forever.

## Security boundaries to preserve

The tool-hosting closure document remains authoritative during migration unless deliberately superseded. In particular:

- host authentication and authorization remain host-owned
- tool registration management remains Dev + Trusted Access
- browser credentials and untrusted identity headers are not forwarded to upstream tools
- upstream validation remains explicit
- mode/tool visibility must fail closed
- route ownership remains explicit
- tool-private and Identity storage remain isolated
- Trusted Preview remains a trusted global control plane rather than inheriting normal mode permissions

Security-sensitive decisions must not be converted into arbitrary editable metadata merely for modularity.

## Planned migration stages

### Stage 0 - Baseline and inventory

- inventory mode-specific references and current ownership
- inventory tool-mode coupling
- inventory deployment-specific values embedded in framework code
- classify tests by subsystem and test type
- record known security boundaries
- compare selected MediaWiki/Wikimedia implementations

No behavior changes.

### Stage 1 - Mode registry

- introduce mode definition/registry contracts
- distinguish standard modes, fallback behavior, and Trusted Preview explicitly
- register current runtime states through the new registry during compatibility migration
- keep the existing `SiteMode` enum temporarily where necessary for compatibility
- add tests using a synthetic standard mode
- migrate value/display-name/editor-scope consumers to registry lookup

Goal: adding a synthetic standard mode proves that identity/editor derivation works without named-mode identity changes.

### Stage 2 - Replace mode-specific shared switches

Migrate shared consumers incrementally, including:

- Trusted Preview target enumeration
- tool visibility and management UI
- stylesheet and presentation resolution
- partial/view-root resolution
- sitemap contributions
- account navigation/editor UI
- content-source mode overrides

Security-sensitive route ownership is migrated behind an explicit contract rather than made implicitly permissive.

### Stage 3 - Physical ownership consolidation

Consolidate mode-owned code so the repository tree communicates ownership. Standard site modes should be visibly separate from framework fallback and global Trusted Preview concerns. A likely logical shape is:

```text
Framework/
    Modes/
        Fallback/
    TrustedPreview/

Modes/
    Professional/
        Definition/
        Presentation/
        Services/
        Views/
        Assets/
    DorksAndDice/
        Definition/
        Presentation/
        Services/
        Views/
        Assets/
```

The exact Razor/static-asset mechanics may require an intermediate layout before eventual Razor Class Library extraction.

### Stage 4 - Framework/deployment separation

- move host/domain mappings to deployment composition
- isolate production-only configuration and infrastructure assumptions
- separate reusable framework registration from this installation's composition
- ensure a standard mode can be hosted under a different domain without modifying the mode module

### Stage 5 - Tool/plugin architecture review

- preserve Tool as the primary application abstraction
- evaluate a plugin/module manifest or registration API as an optional integration method
- support optional tool-to-tool integrations without hard dependencies
- validate hosting-provider boundaries

### Stage 6 - Content architecture review

Compare the existing article/revision/storage system with MediaWiki's page/revision/content separation and content handlers. Adopt only changes that solve concrete coupling or extensibility problems.

Do not add multi-content revisions solely because MediaWiki supports them.

### Stage 7 - Test-suite restructuring

- use existing integration tests as characterization coverage during migration
- separate framework contracts, standard-mode tests, tool-hosting tests, Trusted Preview/security tests, deployment integration tests, and test support
- parameterize repeated mode matrices where that preserves intent
- retain both focused policy tests and end-to-end security wiring tests where they protect different failure modes
- remove tests only when equivalent coverage is demonstrable

### Stage 8 - Extraction proof

Prove that at least one real standard mode has no inappropriate dependency on another standard mode or this deployment's host configuration.

The likely later packaging target is Razor Class Libraries/projects, but project splitting occurs only after the logical boundaries are proven.

### Stage 9 - Security completion

- perform architectural threat-model review against the final repository and deployment configuration
- identify authentication, authorization, role escalation, mode isolation, Trusted Preview, route ownership, content/media access, tool-hosting, proxy, input-validation, and configuration/secrets attack surfaces
- prepare a detailed authorized penetration-test prompt for a Work session
- remediate findings and rerun regression/security tests

## First concrete implementation milestone

Before moving files, replace the current scattered source-of-truth pattern with a registry while preserving behavior.

Known initial consumers include:

- `SiteModeValues`
- `SiteModeMiddleware`
- `SiteModeContext`
- `SiteModePartialResolver`
- `SiteModeStylesheetResolver`
- presentation-module registration/resolution
- `SiteRouteOwnership`
- `ContentSourceRegistry`
- scoped editor-role derivation
- Trusted Preview UI/validation
- sitemap generation
- tool visibility and tool-management mode selection

The registry must become authoritative before physical file moves begin. This avoids moving the current coupling into prettier folders without actually removing it.
