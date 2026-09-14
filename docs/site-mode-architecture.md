# Site mode architecture

The application serves multiple site identities from one ASP.NET Core MVC deployment while keeping framework behavior, authored content, presentation, mode-owned specialization, plugins, and substantial Tools behind explicit boundaries.

## Runtime concepts

The current **normal site modes** are:

- `professional`
- `dorks-and-dice`

Two framework-owned runtime states are intentionally not normal modes:

- **Trusted Preview** — synthetic development/control-plane context used to inspect normal modes and explicitly selected content sources.
- **Fallback** — minimal framework-owned behavior used when no normal site identity/presentation applies.

Normal mode definitions are consumed through stable IDs and `ISiteModeRegistry`. The final persistence mechanism for mode definitions remains an independent deployment/productization decision; generic framework code must not depend on normal modes being permanently defined in C# or in a database.

A normal mode is not limited to presentation metadata. It may own structural behavior, route subtrees, services, persistence, authorization policy composition, and other domain behavior intrinsic to that site. The framework should expose generic extension/ownership mechanisms rather than learning named-mode business concepts.

## Request flow

`SiteModeMiddleware` and the site-mode services establish the active request context.

At a high level:

1. Normalize the request host.
2. Resolve a normal mode from deployment configuration/registry data when the host belongs to one.
3. On a trusted development host, resolve the Trusted Preview state and any explicitly selected normal-mode preview.
4. Resolve content-source composition for the active normal mode, or explicit developer-selected sources in Trusted Preview.
5. Store `SiteModeContext` for downstream routing, authorization, content, presentation, plugins, Tools, and mode-owned behavior.
6. Apply route ownership before exposing a mode-owned route.
7. Return normal 404 behavior when a real host requests a route owned by another mode.

Development preview cookies are diagnostic controls and are ignored as public mode-selection authority on real domains.

## Route ownership

Route ownership is centralized in `Services/Site/SiteRouteOwnership.cs`.

Important public/shared routes include:

- `/` — mode-adaptive database-backed homepage;
- `/articles` and `/articles/{slug}` — mode-aware public content;
- `/resume` and `/resume/{slug}` — Professional-owned aliases/routes;
- `/content/media/...` — shared managed-media transport with visibility enforcement;
- `/site.txt` and `/llms.txt` — public mode-specific text representations;
- `/sitemap.xml` — database-aware public sitemap;
- `/health` — shared deployment health route.

Development/authoring routes are protected separately and are not public mode content.

Normal modes may also claim route subtrees through `SiteModeDefinition.OwnedRoutePrefixes`. These routes are part of the mode's structural surface, not automatically Tools. Professional already uses this mechanism for `/resume`; future mode-native features may use the same ownership contract without adding named-mode branches to generic routing code.

A real Dorks & Dice request for `/resume`, for example, receives normal 404 behavior. Trusted Preview may inspect cross-mode routes when authorized because its purpose is diagnostics, not public tenancy.

## Mode-owned structural and domain behavior

Mode ownership is broader than theming. When a capability is intrinsic to one site's domain, a normal mode may own the implementation and data boundary for that capability rather than promoting it into the generic framework or forcing it into a Tool solely because the capability is substantial.

Mode-owned behavior should follow these rules:

- keep named-mode business concepts out of generic framework services;
- keep mode-specific controllers, services, models, views, persistence, and routes physically attributable to that mode where practical;
- use the framework's stable mode ID, route ownership, authentication, authorization infrastructure, and other generic contracts rather than hard-coded dispatch in shared code;
- preserve an extraction boundary so the mode can later move to another compatible deployment with its required code/data/capabilities;
- expose explicit contracts when separately deployable Tools need to consume mode-owned data or authority.

For Dorks & Dice, campaign membership/authority and account-owned character relationships are examples of domain concepts that may be native to the mode. Rules Core and Block Initiative remain Tools because they have independent application workflows and lifecycle/runtime boundaries. Size alone does not determine the boundary.

## Presentation ownership

Normal modes own visual identity and presentation defaults in addition to any mode-specific structural/domain behavior they legitimately own.

Mode presentation can define values such as:

- title suffix;
- default meta description;
- favicon;
- default meta/social image;
- structured data;
- article-index presentation.

`ISiteModePresentationService` resolves the active normal-mode presentation and falls back to framework presentation when a presentation part is unavailable.

Branding partials remain mode-owned, for example:

```text
Views/SiteModes/{Mode}/Branding/_Header.cshtml
Views/SiteModes/{Mode}/Branding/_Footer.cshtml
```

The shared layout resolves those components before rendering. Mode-specific Razor files should not become central dispatchers that branch across independent site identities.

## Stylesheets and static presentation assets

Shared framework assets live under the normal shared `wwwroot` paths. Mode-specific presentation assets live under:

```text
wwwroot/site-modes/{mode}/
```

Current legitimate Professional static assets include its stylesheet and favicon. Current legitimate Dorks & Dice static assets include its stylesheet/branding assets.

Authored media is different: résumé PDFs, credentials, article images, homepage headshots, and similar page-owned media belong in the managed content-media system rather than source-tree static copies.

The legacy Professional file-backed homepage/resume implementation and its duplicate static résumé/contact/credential/headshot assets have been retired. Professional metadata/structured data now references the managed homepage headshot. The Professional favicon remains static because it is presentation identity rather than authored content.

Shared CSS must contain only intentionally shared behavior. Mode-specific selectors belong in the corresponding mode stylesheet.

## Unified content model

Navigable authored content uses one revision-oriented model. Context is represented by tags rather than separate page stores.

Current important contexts include:

```text
homepage
article
project
experience
```

A page may participate in multiple contexts. A Project may also be an Experience record without duplicating its stable identity/detail page.

`ContentItem` carries shared content fields including:

- stable content ID;
- public slug;
- title/subtitle/summary/date/link fields;
- structured detail-header metadata;
- optional context-specific presentations;
- tags;
- visible normal-mode IDs;
- body format/body;
- revision identity.

Mode visibility remains structured data because it is an eligibility boundary rather than a user-facing categorization tag.

Mode-owned operational/domain data is not required to fit the authored content model. Mutable campaign membership, character associations, tool authority, or similar application state should use an appropriate mode-owned relational/domain store rather than being represented as authored Markdown pages merely for architectural uniformity.

## Revision storage

Content uses stable page identity plus immutable revisions. The schema includes:

```text
content_page
content_revision
content_revision_tag
content_revision_mode
content_asset
content_page_asset
content_page_asset_dependency
content_revision_asset
content_redirect
```

`content_page` owns stable identity, current slug, and current-revision pointer. Saving creates a new immutable revision rather than overwriting the previous one.

Revision tags and visible modes preserve historical context. Redirects target stable page identity, avoiding redirect chains when slugs change again.

The supported authored body format is Markdown. `ContentBodyRenderer` uses the shared rendering/sanitization boundary and supports constrained application-owned directives/components. Authored content can select an installed capability but can not inject arbitrary executable code.

## Database-backed homepages

A normal mode homepage is ordinary content with:

```text
tag: homepage
visible mode: <normal mode ID>
```

Homepage resolution is:

```text
request
  -> active normal mode
  -> composed configured content sources
  -> exactly one eligible current page tagged homepage
       -> shared database-backed homepage renderer
  -> framework fallback if no normal homepage/module applies
```

Both current normal sites use this database-backed path:

- `professional-home` is authoritative in External;
- `dorks-and-dice-home` is authoritative in External.

Their old compiled normal-mode homepage fallbacks are retired. The framework fallback remains registered because it is a framework concern, not authored normal-mode content.

`/resume` is a Professional-owned alias through the same `ISiteModeHomeService`; it does not have a second résumé source of truth.

## Page composition and plugins

Dynamic blocks that are small, in-process, and reusable are exposed through constrained page-component/plugin boundaries.

Current examples include:

- `content-collection` / Professional portfolio presentations;
- `discord-widget`;
- `minecraft-server-status`.

The Professional homepage uses database-backed Experience and Project records through the portfolio collection component rather than embedding duplicate records in homepage Markdown.

The Dorks & Dice homepage uses installed Discord and Minecraft components. Minecraft host/port/protocol/cache configuration remains deployment-owned; authored Markdown can select the component but can not redirect the service to arbitrary network targets.

A substantial capability with an independent application workflow, data/runtime lifecycle, or separate-deployment value belongs under Tools. A substantial capability that is instead intrinsic mode-owned domain behavior may remain in the mode. Neither page components nor the generic framework should be used as a dumping ground for mode-specific application logic.

## Managed media

Managed media has stable identity independent of page revisions and is exposed through:

```text
/content/media/{assetKey}/{fileName}
```

The media system supports validated image/PDF uploads, stable media URLs, current-page/revision references, same-source ownership/attachments, and source-qualified cross-source dependencies.

The media library can replace the bytes behind an existing asset while retaining its stable key, canonical filename, URL, page attachments/dependencies, and revision references. Identical bytes are a no-op, and replacement must remain within the validated media type boundary.

Authoring surfaces expose page dependency information before replacement. PDF assets support inline preview plus an explicit Open PDF action.

Public media is served only when current eligible content in the active mode/source composition references the asset. Merely retaining an orphaned asset or an old historical revision does not make the media public.

## Content sources

Content stores are configured as named sources with stable source keys. Each source specifies its provider and named connection string.

Sources are composed in order. Later sources can override earlier records with the same stable content identity; slug collisions across different identities are resolved according to the content-source rules rather than incidental query order.

`External` is the live published content source in this deployment. `Local` is an authoring workspace used when needed; it may validly be empty. After the homepage/content promotion work, the current Local workspace is empty while published content is authoritative in External.

Normal modes receive deployment-defined source composition. Trusted Preview does not imply production source policy: an authorized developer may explicitly select which configured source or sources to inspect.

Cross-source media dependencies persist the source key alongside the asset key, so source keys are data identifiers and can not be casually renamed without migration.

## Authoring workflow

The Development authoring surface supports revision-oriented editing without requiring source-code changes for ordinary authored content.

Permanent authoring capabilities include:

- list/search configured source content;
- create pages;
- edit structured metadata and Markdown;
- save immutable revisions;
- view revision history;
- detect stale concurrent saves;
- upload and attach managed media;
- inspect media dependencies;
- replace managed media in place;
- deliberately move a single page between configured sources when safe.

The single-page Move operation copies the complete page/revision graph and applicable media/dependency/redirect information before removing the source copy. It refuses unsafe target conflicts rather than overwriting published history.

Bulk transfer was used as a one-time migration mechanism and is not part of the permanent normal authoring UX.

Direct authoring of a configured source is allowed only through the normal authorized source-aware editor path. Normal Editors remain mode-scoped; Trusted Preview/Dev authority is separate and must not be inferred from ordinary Editor access.

## Visibility and listing

Listing and access are separate concepts.

Unlisted content:

- is hidden from normal indexes;
- may remain directly addressable when otherwise eligible;
- uses noindex/nofollow presentation;
- may be inspected in Trusted Preview when the authorized preview setting allows it.

Normal public requests first pass source composition and mode eligibility. User-facing search/tag/filter controls operate only on the resulting eligible set and are never a substitute for access control.

## Public text and sitemap views

Public crawler/AI-facing text is generated at request time from the same current public catalog as the site rather than from a separate résumé JSON/static export.

- `/site.txt` — fuller current public text representation;
- `/llms.txt` — compact discovery/index representation.

Synthetic Trusted Preview/Development context and unlisted/private content are excluded from these public exporters.

The sitemap is likewise assembled from current database-backed public routes/content rather than only compiled/static routes. Mode-owned structural routes that should be public/indexed belong in the mode's intrinsic sitemap metadata rather than being inferred from controller discovery.

## Identity and authorization boundary

Authentication and generic authorization infrastructure are framework concerns. Mode-specific application authority may be derived from mode-owned domain relationships without turning those relationships into global account roles.

For example, a Dorks & Dice account may be a DM in one campaign, a player in another, both in a third, and neither in others. Campaign authority therefore belongs to the Dorks & Dice campaign domain rather than ASP.NET global roles. Global site roles remain reserved for genuinely account-wide/framework or mode-wide authority.

Trusted administrative/development access is a separate condition and remains constrained by the configured trusted-access boundary. Real public host behavior does not accept preview cookies as authority.

Authoring, media mutation, source selection, account administration, and Tool administration remain protected operations.

## Tools

Tools are larger applications/workflows that may have their own data, lifecycle, or containerized/separate runtime.

The framework owns Tool registration, exposure, health/proxy/hosting boundaries, and mode-aware access. A normal mode can expose a Tool without absorbing the Tool's implementation into the mode definition. Conversely, a normal mode does not have to turn its own intrinsic domain model into a Tool merely because that model is substantial.

Dorks & Dice uses this distinction deliberately: Rules Core owns rules/source normalization and campaign rule overlays as a separately deployable Tool; Block Initiative owns encounter initiative workflow as a separately deployable Tool; the Dorks & Dice mode may own the campaign/account/character relationship and authorization domain that those Tools consume through explicit contracts.

## Extension rules

When adding another normal mode:

1. Give it a stable registered ID and deployment-owned host/source configuration.
2. Supply the presentation/branding/plugins/Tools and mode-owned structural/domain modules that identity actually requires.
3. Author ordinary editorial pages/homepage through the content system rather than duplicating framework controllers or compiled content stores.
4. Keep mutable operational/domain state in an appropriate mode-owned store instead of forcing it into authored content.
5. Use generic route/content/media/identity/authorization infrastructure wherever possible, but do not generalize mode-specific business concepts into the framework without a genuine reusable requirement.
6. Use a Tool when independent application lifecycle, runtime/data ownership, or separate deployment is part of the desired boundary.

When a mode later needs to split into a separate deployment, stable mode IDs, source ownership, mode-owned code/data boundaries, plugin/Tool boundaries, and independently owned presentation assets provide the extraction seams.

## Current convergence state

For this refactor cycle:

- both current normal-mode homepages are External database content;
- Local is a valid empty authoring workspace;
- both old compiled normal-mode homepage fallbacks are retired;
- the old file-backed Professional résumé/homepage subsystem is retired;
- authored Professional media uses managed content assets;
- Dorks & Dice live Discord/Minecraft behavior uses plugins;
- runtime `/site.txt`, `/llms.txt`, and sitemap follow current database content;
- framework Fallback and Trusted Preview remain separate framework concerns.

Remaining work from that convergence cycle is validation/audit and final refactor integration, not another homepage storage architecture change. Future mode specialization may add native structural/domain functionality without reopening the completed homepage/content convergence decision.
