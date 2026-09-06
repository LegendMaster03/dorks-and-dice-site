# Normal mode portability and extraction

## Purpose

A normal site mode is considered portable when its site identity can be added to another compatible framework installation, or run as the only normal mode in a separate deployment, without editing generic framework code.

This document defines the portability boundary that the current refactor must preserve. It deliberately does not choose whether the final authoritative mode definition is compiled C# or persistent runtime data. Both models must be able to feed the same runtime contracts.

## Portable mode inventory

A portable normal mode may consist of the following independently owned pieces:

1. **Mode registration**
   - stable mode ID;
   - display name;
   - view/asset ownership metadata;
   - owned route prefixes and narrow compatibility asset paths;
   - intrinsic sitemap paths;
   - presentation/navigation defaults that are structural rather than authored content.

2. **Authored content and media**
   - content page stable IDs and slugs;
   - complete revision history when history is being transferred;
   - tags and mode visibility metadata;
   - redirects;
   - page-owned managed media;
   - source-qualified Global media dependencies;
   - exact revision media references.

3. **Presentation/theme assets**
   - mode stylesheet or theme package;
   - branding partials or their future equivalent;
   - presentation-owned favicon/logo assets;
   - other non-content visual resources owned by the mode.

4. **Required plugins**
   - stable plugin IDs;
   - required versions/version ranges when version negotiation is introduced;
   - plugin-owned views/static assets and executable assembly/deployment artifact.

5. **Required Tools**
   - stable Tool IDs required by the mode;
   - mode enablement/configuration that is safe to export;
   - exportable mode-owned Tool data where the Tool contract explicitly supports it.

6. **Optional mode-owned executable code**
   - only when the chosen mode-definition model requires compiled specialization beyond installed plugins/Tools.

## Deployment-owned state that must not travel with a mode

The following belongs to the destination deployment and must not be embedded in a portable mode package:

- domain names and canonical host bindings;
- database server addresses and connection strings;
- passwords, tokens, API keys, certificates, or other secrets;
- trusted-proxy or trusted-network configuration;
- machine-local filesystem paths;
- container/network addresses such as the Minecraft server host and port;
- production data-protection keys;
- SMTP credentials;
- reverse-proxy configuration.

A package may declare that a capability requires deployment configuration, but it may not carry the protected value.

## Procedure: add a new normal mode

The current framework boundary supports a new mode without adding a value to the legacy `SiteMode` enum. The enum is migration compatibility only.

1. Choose a stable lowercase mode ID. Treat it as persistent identity, not display text.
2. Supply a `SiteModeDefinition` through the deployment's mode-registration source.
3. Supply presentation/theme pieces required by the mode. Do not add named-mode branches to generic framework services.
4. Install any required plugins and Tools independently, then enable/compose them for the mode.
5. Configure the content source composition and authoring source for the destination deployment.
6. Import or create the mode's content, revisions, redirects, and managed media through the content-layer transfer/import contract.
7. Configure host/domain bindings and protected infrastructure values in deployment configuration.
8. Add mode-scoped roles only through the existing stable-ID scoped-role system.
9. Verify:
   - host resolves to the registered stable ID;
   - normal routes and mode-owned routes are isolated correctly;
   - another mode can not load this mode's private static asset area;
   - content source precedence and `VisibleInModes` work for the new stable ID;
   - Editor authority remains mode scoped;
   - Trusted Preview can inspect the mode without becoming its authorization authority;
   - required plugin components resolve;
   - public text/sitemap output contains only public content for the mode.

A new mode that has no `SiteMode` enum value is expected to work. Tests in `SiteModeRegistryTests` exercise registry lookup, route ownership, stylesheet selection, content/scoped-editor capability, and Trusted Preview selection for such a mode.

## Procedure: spin an existing mode into a separate deployment

1. **Freeze the source boundary for the operation.** Record the mode stable ID, content sources being exported, current revisions, required plugin IDs, Tool IDs, and presentation/theme version.
2. **Provision a compatible framework deployment.** Do not copy production secrets from the original installation.
3. **Register the same stable mode ID** in the destination. It may be the destination's only normal mode.
4. **Install required plugins/Tools** before importing content that invokes them.
5. **Copy presentation/theme ownership** for the mode. Framework fallback and Trusted Preview remain framework-owned and are not exported as part of the mode.
6. **Transfer content through the content model**, preserving stable content identity, revision history, redirects, page-owned media, and declared Global dependencies. Do not copy a raw database merely to bypass source/provider boundaries unless the destination is intentionally a database-level clone.
7. **Resolve Global dependencies.** Either install/copy the required Global media/content source or deliberately convert dependencies into destination-owned equivalents through a migration operation.
8. **Configure destination infrastructure**: content/Identity databases, host names, reverse proxy, trusted networks, email, game-server endpoints, and secrets.
9. **Validate isolation** with the original mode absent. Generic framework services must start and render the extracted mode without referencing Professional or Dorks & Dice by name.
10. **Validate public behavior**: homepage, content routes, media, redirects, plugins, Tools, `/site.txt`, `/llms.txt`, sitemap, authentication, and mode-scoped authorization.
11. Only after destination verification should the source installation remove the mode registration or its owned data.

## Portability proof required by this refactor

The refactor does not need to create a production export archive or physically split this repository. It must prove the boundary by tests and structure:

- a third normal mode with no legacy enum value can be registered;
- generic routing/presentation/content authorization works from its stable definition;
- Trusted Preview can select it without granting normal-mode authorization;
- its assets remain isolated from other normal modes;
- content visibility can use its stable mode ID;
- no deployment secret is required by `SiteModeDefinition`;
- framework fallback and synthetic Trusted Preview remain outside the normal-mode registry;
- plugin requirements are represented independently of authored content;
- the documented extraction inventory is sufficient to identify what must move and what must be recreated as deployment configuration.

A future formal package/export format should version this inventory and automate the transfer, but that is intentionally outside the current refactor cycle.

## Known transitional compatibility

The current application still contains a `BuiltInSiteModes` facade and a legacy `SiteMode` enum for callers that have not yet moved completely to stable IDs. Those are compatibility boundaries, not the desired long-term registration source. New framework work must not add additional dependencies on them.

The remaining registration cleanup is complete when the deployment supplies normal mode definitions through one explicit registration source and generic runtime code consumes only `ISiteModeRegistry`/stable IDs. At that point the compatibility facade can be limited to migration/tests or removed when the final enum callers are gone.
