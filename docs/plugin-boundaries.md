# Plugin boundaries

Plugins are the in-process executable extension mechanism for reusable site capabilities that are smaller than standalone Tools. They are distinct from normal authored content, themes, modes, mode-owned domain behavior, and Tool applications.

## Ownership split

- **Core framework** owns content storage/revisions, Markdown rendering, page composition, component invocation, identity/authorization infrastructure, mode resolution, and Tool hosting.
- **Themes** own visual identity and broadly reusable presentation styling.
- **Plugins** contribute installed executable page components, content presentations, adapters, and similar reusable in-process capabilities.
- **Tools** remain substantial applications/services with an independent runtime/data lifecycle and may be containerized or hosted separately.
- **Modes** compose these capabilities and may also own structural/domain behavior intrinsic to that site. Generic framework code should not absorb named-mode business concepts merely to avoid mode specialization. Whether normal mode definitions ultimately live in C# or persistent runtime data remains a separate decision.

Authored page content may select installed capabilities by stable keys, but it can not introduce executable code. Installing or upgrading a plugin remains a deployment operation. Editing a page to use an already-installed plugin does not require a restart.

## Plugin contract

Each installed plugin has a manifest with:

- stable ID
- display name
- version
- declared plugin dependencies

Startup validates duplicate IDs and missing dependencies before the plugin registers its services. The runtime catalog exposes installed manifests so future mode composition/export can validate required plugins.

Plugin manifests and authored page configuration must not contain deployment secrets. Connection strings, credentials, network addresses, ports, and other protected infrastructure configuration remain deployment-owned.

## Page composition

The shared page composer supports two extension paths:

1. framework-owned components such as `content-collection`;
2. plugin-contributed components such as `discord-widget` and `minecraft-server-status`.

Parameterized component invocations use quoted key/value parameters and must occupy their own Markdown line. Parameterless component names may also be claimed by an installed page-component definition. Existing parameterless Markdown directives remain supported by the body renderer when no page component claims that name.

Content collection querying stays framework-owned. Specialized presentation can be supplied by a plugin. This keeps database content portable while allowing an extracted site to carry its required presentation plugin as an explicit dependency.

## Current plugins

### `professional-portfolio`

Provides the `professional-experience` and `professional-projects` content-collection presentations. It does not own the Experience or Project records; those remain normal database content queried through the core content catalog. The experience/project card partials used by these presentations are plugin-owned so the plugin does not depend on the Professional mode folder for its primary rendering behavior.

### `discord-widget`

Provides the `discord-widget` page component. The Discord server ID, theme, and accessibility/display title may be selected by authored content, but the component constructs the trusted Discord widget URL itself rather than granting arbitrary iframe capability. For example:

```markdown
{{discord-widget server-id="1281714470799806545" theme="dark" title="Dorks & Dice Discord Server"}}
```

### `minecraft-server-status`

Provides the `minecraft-server-status` page component and owns registration of the existing Minecraft status-query service. The working Minecraft protocol implementation remains unchanged; the plugin is the executable composition boundary that exposes its result to authored pages.

The component is intentionally parameterless:

```markdown
{{minecraft-server-status}}
```

Host, port, protocol version, query timeout, and cache policy remain deployment configuration under `GameServers:Minecraft`. Authored Markdown can not redirect the status query to an arbitrary endpoint.

Minecraft is the only currently supported game-server status implementation. Hytale support was explored previously. Similar status endpoints may exist, but no sufficiently documented or reliable interface was found, and the known alternatives would have required server modification. Hytale status integration is therefore deferred and is not part of the current supported feature set.

## Plugins, Tools, and mode-owned features

Use a plugin when the capability is a small reusable in-process extension whose useful surface is composition inside an existing page or framework workflow.

Use a Tool when the capability has an independent application workflow, data/runtime lifecycle, or separate-deployment value. A Tool may be large, but size alone is not the defining criterion.

Keep a capability mode-owned when it is intrinsic to one normal site's domain and its routes, persistence, authorization relationships, and extraction boundary naturally belong to that mode. Do not create a plugin solely to hide mode-specific business logic behind a generic extension name, and do not create a Tool solely because a native mode feature is substantial.

Minecraft server status fits the plugin boundary because its useful behavior is a compact reusable status query and embedded presentation; it has no meaningful standalone workflow. Rules Core and Block Initiative fit the Tool boundary because they are separately deployable applications with independent domain/runtime lifecycles. The Dorks & Dice campaign/account/character relationship can fit the mode-owned boundary because it defines native Dorks & Dice site state and authority consumed by multiple Tools.
