# Plugin boundaries

Plugins are the in-process executable extension mechanism for reusable site capabilities that are smaller than standalone Tools. They are distinct from normal authored content, themes, modes, and Tool applications.

## Ownership split

- **Core framework** owns content storage/revisions, Markdown rendering, page composition, component invocation, identity/authorization, mode resolution, and Tool hosting.
- **Themes** own visual identity and broadly reusable presentation styling.
- **Plugins** contribute installed executable page components, content presentations, adapters, and similar in-process capabilities.
- **Tools** remain substantial applications/services with their own runtime lifecycle and may be containerized or hosted separately.
- **Modes** compose these capabilities. Whether normal mode definitions ultimately live in C# or persistent runtime data remains a separate decision.

Authored page content may select installed capabilities by stable keys, but it can not introduce executable code. Installing or upgrading a plugin remains a deployment operation. Editing a page to use an already-installed plugin does not require a restart.

## Plugin contract

Each installed plugin has a manifest with:

- stable ID
- display name
- version
- declared plugin dependencies

Startup validates duplicate IDs and missing dependencies before the plugin registers its services. The runtime catalog exposes installed manifests so future mode composition/export can validate required plugins.

Plugin manifests and authored page configuration must not contain deployment secrets. Connection strings, credentials, and other protected infrastructure configuration remain deployment-owned.

## Page composition

The shared page composer supports two extension paths:

1. framework-owned components such as `content-collection`;
2. plugin-contributed components such as `discord-widget`.

Parameterized component invocations use quoted key/value parameters and must occupy their own Markdown line. Parameterless component names may also be claimed by an installed page-component definition. Existing parameterless Markdown directives remain supported by the body renderer when no page component claims that name.

Content collection querying stays framework-owned. Specialized presentation can be supplied by a plugin. This keeps database content portable while allowing an extracted site to carry its required presentation plugin as an explicit dependency.

## Current plugins

### `professional-portfolio`

Provides the `professional-experience` and `professional-projects` content-collection presentations. It does not own the Experience or Project records; those remain normal database content queried through the core content catalog.

### `discord-widget`

Provides the parameterless `{{discord-widget}}` page component. The iframe URL comes from deployment configuration (`Discord:WidgetUrl`), not from authored Markdown, so editors do not gain arbitrary iframe capability.

## Tools versus plugins

Game Server Monitoring and Campaigns fit the Tool boundary better than the plugin boundary because they are domain applications/services with their own data or runtime behavior. A future generic Tool page-surface integration can allow authored pages to embed an approved compact Tool surface without moving the Tool's implementation into the main site process.
