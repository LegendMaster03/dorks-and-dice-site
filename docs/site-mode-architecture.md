# Site Mode Architecture

This project uses a site mode architecture so one ASP.NET Core MVC codebase can serve multiple site identities from
different domains without duplicating the whole application.

The current modes are:

- `Professional`: Kyle Barnett professional resume, portfolio, articles, and professional-owned assets.
- `DorksAndDice`: group-facing Dorks & Dice site experience.
- `Development`: local-only preview mode used for testing shared routes, mode switching, content sources, and unlisted content.
- `Unassigned`: bare-bones fallback for domains that are connected to the app but not mapped to a mode.

## Goals

- Keep one deployable codebase while the site identities are still closely related.
- Make ownership explicit so professional pages, Dorks & Dice pages, and shared pages do not blend accidentally.
- Allow shared infrastructure for layout, routing, content handling, static files, and deployment.
- Preserve a future path to split one mode into a separate app if it grows enough to justify that.
- Keep content storage replaceable and composable so the site can add databases or move a database off-host without changing the content model.

## Request Flow

`SiteModeMiddleware` resolves the active mode for each request.

1. Normalize the request host.
2. Check the host against configured professional, Dorks & Dice, and development host lists.
3. On development hosts, read the selected preview mode from `DevelopmentPreviewSiteMode`.
4. On development hosts, read the unlisted-content flag and any explicit content-source selection.
5. Store the resulting `SiteModeContext` in `HttpContext.Items`.
6. Check the requested path through `SiteRouteOwnership`.
7. For real domains, re-execute blocked routes as a normal 404.
8. For development hosts, keep the route inspectable and show a warning in the development ribbon.

Development cookies are ignored on real domains. Query strings are not trusted as a public mode-switching mechanism.

## Route Ownership

Route ownership lives in `dorks-and-dice-site/Services/Site/SiteRouteOwnership.cs`.

Current rules:

- `/` is mode-adaptive.
- `/articles` and `/articles/...` are mode-adaptive.
- `/resume` and `/resume/...` are Professional-owned.
- `/development/content` is development-host-only authoring infrastructure.
- `/health`, static framework assets, and shared error routes are shared exceptions.
- `/site-modes/professional/...` is Professional-owned.
- `/site-modes/dorks-and-dice/...` is Dorks & Dice-owned.
- `/site-modes/unassigned/...` is shared fallback asset space.

`Development` mode can inspect all routes locally, but shared routes still need explicit Development handling when more
than one mode could reasonably own the route.

## Views

Mode-owned views live under `dorks-and-dice-site/Views/SiteModes/`.

Examples:

- `Views/SiteModes/Professional/Home.cshtml`
- `Views/SiteModes/Professional/Resume/`
- `Views/SiteModes/DorksAndDice/Home.cshtml`
- `Views/SiteModes/Development/_DevelopmentTools.cshtml`
- `Views/SiteModes/Unassigned/Home.cshtml`

Unified content detail rendering lives under `Views/Content/`. The article index remains under `Views/Articles/`, but
individual Project, Experience, and Article detail pages no longer require one Razor file per page.

## Branding And Mode-Specific Components

Each independently rendered mode-specific component has its own Razor partial. Branding currently uses:

```text
Views/SiteModes/{Mode}/Branding/_Header.cshtml
Views/SiteModes/{Mode}/Branding/_Footer.cshtml
```

The shared layout asks `ISiteModePartialResolver` for the active mode's header and footer paths and renders those partials
directly. The resolver checks whether the requested component exists for the active mode. When it does not, the resolver
returns the matching component under `Views/SiteModes/Unassigned/` before Razor rendering begins.

Unassigned components and Unassigned-owned static assets are the known fallback pieces. Any mode may load those fallback
assets when a resolved fallback component needs them. This is separate from the Unassigned fallback page itself, which is
the home page for unmapped hosts.

Mode-specific Razor files must not act as dispatchers by branching over a component identifier. In particular, avoid a
single partial with an `if`, `else if`, or `switch` that chooses between independently rendered components such as a
header and footer. Component selection belongs in a resolver or service; the selected partial should contain only the
markup for that component.

Use C# presentation or branding objects for structured metadata and behavior such as site names, theme identifiers,
logo paths, and default descriptions. Keep substantial HTML in Razor partials rather than constructing markup in C#.

This standard provides:

- one renderable responsibility per partial
- mode selection before rendering
- component-specific Unassigned fallback
- independent testing and maintenance of each component
- a direct extension path when another mode-specific component is added

## Presentation Modules

Non-view presentation defaults live under:

```text
dorks-and-dice-site/Services/Site/ModePresentation/
```

Each mode can define:

- title suffixes
- default meta descriptions
- favicon paths
- article index copy
- article filter visibility policy

`SiteModePresentationService` calls the active mode module and falls back to the same function on the Unassigned module
when a mode module is missing or explicitly reports that a presentation part is unavailable.

## Static Assets

Shared framework assets remain at root paths:

- `wwwroot/css`
- `wwwroot/js`
- `wwwroot/lib`
- `wwwroot/robots.txt`

`wwwroot/favicon.ico` remains the Unassigned fallback favicon. Mode-specific favicons live with other mode-owned assets
and are emitted by the shared layout through `ISiteModePresentationService`.

Mode-owned assets live under:

```text
wwwroot/site-modes/{mode}/
```

Professional examples:

- `wwwroot/site-modes/professional/images/profile/kyle-headshot.jpg`
- `wwwroot/site-modes/professional/images/articles/`
- `wwwroot/site-modes/professional/images/logos/`
- `wwwroot/site-modes/professional/files/kyle-resume.pdf`
- `wwwroot/site-modes/professional/files/kyle-resume.txt`

This means Dorks & Dice and Unassigned domains cannot directly load Professional-owned media or files.

## Stylesheets

The layout always loads the shared foundation first:

```text
wwwroot/css/site.css
```

Shared CSS contains only intentionally shared behavior and components, including accessibility focus states, base
document behavior, shared article controls, and shared image-modal behavior. It must not define a site identity or contain
Professional-only, Dorks & Dice-only, or Development-tool component rules.

Mode-owned stylesheets are:

```text
wwwroot/site-modes/professional/css/site.css
wwwroot/site-modes/dorks-and-dice/css/site.css
wwwroot/site-modes/development/css/site.css
```

`ISiteModeStylesheetResolver` selects the stylesheet paths before Razor renders them:

- `Professional` loads the Professional stylesheet after shared CSS.
- `DorksAndDice` loads the Dorks & Dice stylesheet after shared CSS.
- `Development` loads only the Development stylesheet after shared CSS.
- `Unassigned` loads no mode stylesheet and depends only on shared CSS.

The absence of an Unassigned stylesheet is intentional. Unassigned is the visual and structural fallback and must remain
independent only on shared infrastructure.

On a development host, the selected preview mode stylesheet loads first and the Development stylesheet loads afterward
for the preview toolbar and diagnostics. For example, Dorks & Dice preview loads:

```text
shared CSS
Dorks & Dice CSS
Development CSS
```

CSS ownership rules:

- Professional resume, portfolio, contact, credential, project, dark-mode, responsive, and print rules belong to Professional.
- Discord presentation, campaigns, game servers, and Dorks & Dice visual identity belong to Dorks & Dice.
- Development ribbon and diagnostic-tool rules belong to Development.
- Rules used intentionally across site identities belong to shared CSS.
- Do not place mode-owned selectors in shared CSS merely because the shared layout loads it everywhere.

## Unified Content System

Projects, Experience entries with detail pages, and Articles use one content model. Their distinction is the context in
which a page is listed, not a separate storage or controller type.

`ContentItem` provides the shared fields needed by those contexts, including:

- stable `Id`
- public `Slug`
- title, subtitle, summary, dates, images, links, and detail-header metadata
- optional context-specific presentations
- `VisibleInModes`
- tags
- revision ID, body format, and body

Context is many-to-many and is represented by the existing tag system:

```text
project
experience
article
```

A single page can therefore be both a Project and Experience entry without duplicating its detail page or stable
identity. Skyblivion, Skywind, and Safe Future are examples of content that can be presented in more than one context.

Context tags and internal tags are not exposed as normal user-facing tags. `_internal:unlisted` is the current internal
listing-state tag. Listed is the default state, so there is no positive `listed` flag that every normal page must carry.

`VisibleInModes` remains typed data rather than a free-form tag because it is an access/identity rule. Development detail
preview may inspect content that is not eligible for the selected real mode, but real domains still enforce mode
eligibility before rendering.

### Revision Storage

The content database uses a revision-oriented schema inspired by MediaWiki's separation of page identity from page
revision history:

```text
content_page
content_revision
content_revision_tag
content_revision_mode
```

`content_page` owns the stable content key, current slug, and pointer to the current revision. `content_revision` stores
immutable revision content and an optional parent revision. Tags and visible modes are attached to each revision so an
old revision remains a complete historical snapshot.

The current page is therefore selected through `page_current_revision_id`; saving does not overwrite the previous
revision.

The body format is currently `markdown`. `ContentBodyRenderer` uses Markdig and supports registered `{{directive}}`
blocks for application-owned dynamic sections. This keeps ordinary authoring content out of Razor while preserving a
controlled extension point for pages that need live application data.

Rendered content is treated as trusted site-authored content. Markdown output is emitted as HTML so existing rich HTML
and application-owned directives can render correctly. Do not connect an untrusted external authoring source to the
published catalog without adding sanitization or a stricter Markdown pipeline first.

The Professional resume still uses `Content/Resume/resume.json` for resume-only structures that are not navigable detail
content, such as contact links, education, awards, skills, and leadership. Project and Experience detail records are no
longer stored there.

### Database Sources

Content storage is configured as named database sources under `ContentStorage:Sources`. A source owns:

- a stable source key
- a display name
- a provider name
- a named connection string

The repository can read more than one source during the same request. Source order matters: sources are composed from
base to override, and a later source replaces an earlier page with the same stable content ID. If two different stable
IDs claim the same slug, the later source owns that slug in the composed catalog.

`ContentStorage:GlobalSources` is the ordered global source list.

Only real site identities receive per-mode source-list differences:

- `Professional` starts from the global list unless `InheritGlobal` is disabled, then applies its configured `Remove` and `Add` entries.
- `DorksAndDice` follows the same rule.
- `Unassigned` does not have a mode-specific source layer. It uses the global list exactly.
- `Development` does not inherit the global list and does not have a configured mode-specific list. Its source set is selected manually in the development UI.

This separation is intentional. Unassigned is a fall-through identity and should represent the global default directly.
Development is a diagnostic environment and should not accidentally imply a production content-source policy.

The current `Local` and `External` source definitions intentionally point to the same SQLite database. That is redundant
now, but it preserves the connection boundary. In production, `External` should become the real published content
database and remain the global source. `Local` is for localhost authoring and test content; once the external database
is active, deploy should stop publishing the local authoring database to the server. SQLite is the only configured
provider in the current build; adding another provider is a storage-adapter concern rather than a content-model change.

The local authoring database is selected separately through `ContentStorage:AuthoringSource`. The development editor
writes revisions only to that source instead of writing through the composed read catalog.

### Authoring Workflow

Development hosts expose a lightweight wiki-style authoring surface at `/development/content`.

It can:

- list locally authored content
- create a new stable page
- edit structured revision metadata
- edit Markdown body content
- preview rendered body content
- save a new revision without destroying the previous revision
- show revision history
- reject a stale save when the current revision changed after the editor was opened

The editor is development-host-only. It is not a public CMS and is not exposed on production domains.

The current editor deliberately exposes structured metadata as JSON rather than building a large administrative UI too
early. A richer editor can later sit on the same revision model without changing stored content.

The design takes MediaWiki as reference and inspiration rather than attempting to duplicate MediaWiki wholesale. The
important ideas retained here are stable page identity, separate revisions, a current-revision pointer, linkable page
content, and an authoring path that does not require application source changes for every new page.

The Dorks & Dice mode may eventually use the same content foundation for campaign knowledge management. That direction
can combine wiki-style pages with selected campaign-planning and linked-note ideas such as characters, locations,
factions, session notes, timelines, cross-links, and private/public visibility boundaries.

### Listing And Eligibility

Unlisted content:

- is identified by `_internal:unlisted`
- is hidden from normal indexes
- remains accessible by direct URL when eligible for the current mode
- renders `noindex, nofollow`
- can be exposed in development preview through the unlisted-content toggle

`Development` mode ignores `VisibleInModes` for local inspection, but Development has no automatic content database
selection. The developer must select the database source or sources to inspect.

Mode eligibility is an internal content gate, not a user-facing filter. After the current mode and source composition have
determined which content is eligible, indexes can expose normal user filters such as search, category, and tags.

## User-Facing Filters

User-facing filters operate only on content that has already passed source composition, the active mode's ownership, and
visibility rules. They are a convenience layer for small curated indexes, not a replacement for mode access control or
content storage.

Current user-facing filters include:

- project tags on the Professional resume project list
- article search
- article categories
- article tags

Projects and Articles now use the same generic `data-content-*` JavaScript binding. Context-specific Razor markup decides
which controls and fields are present, while one filter implementation handles search, tags, categories, status counts,
and supported ordering behavior.

These filters are intentionally separate from mode eligibility. A visitor can narrow visible content, but cannot use a
filter control to reveal content that is not available to the current mode.

Search inputs can suggest known tags through the browser's datalist behavior. Search terms follow a small advanced
tag-query subset adapted to this site's content model:

- `architecture web-development` requires both terms.
- `architecture -game-dev` requires `architecture` and excludes `game-dev`.
- `~architecture ~data-science` matches either term.
- `web-*` matches tags or text that begin with `web-`.
- `tag:architecture`, `category:"Technical Investigation"`, `title:website`, and `text:client` search specific fields.
- `order:title`, `order:date`, `order:tagcount`, and `order:featured` sort supported lists.

The search syntax operates only after source selection, route ownership, and mode visibility have already limited the
available content.

Taxonomy should remain intentional. New tags should be added when they improve an existing listing, query, or future
content relationship rather than merely because the tag system can represent them.

## Local Development Preview

Development hosts are configured in `SiteModeOptions`.

The development ribbon:

- shows the selected preview mode
- allows switching between Dorks & Dice, Professional, and Development
- has an Articles submenu
- moves the unlisted-content toggle into the Articles submenu
- links to the development content editor from the Articles submenu
- lists every configured content database source and allows each one to be enabled or disabled independently
- applies changes immediately without an apply button
- stores preview mode, unlisted state, and explicit source selection in cookies
- shows route mismatch warnings when the selected mode could not access the current route on a real domain

When a development host previews Professional or Dorks & Dice and no explicit source selection has been made, the
selected real mode's configured source list is used. Once the developer changes a source toggle, that explicit source set
becomes the development selection. Selecting Development itself starts with no inherited database sources; its sources
must be chosen manually.

The selected preview mode is the source of truth for branding, layout, navigation, mode-owned CSS, and mode visibility
while on a development host. Development-tool CSS is added as an overlay after the selected mode stylesheet. Content
source selection is deliberately a separate development control so storage composition can be tested independently from
visual/site-mode selection.

## Future Extraction Path

This architecture intentionally avoids premature separation, but it does not block future separation.

Because each mode already owns its pages, branding, asset paths, stylesheets, access rules, and content-source policy, a
mode can later be extracted into a separate application with clearer boundaries than a fully blended site would provide.

The current shape is therefore a middle ground:

- One deployment and one codebase while the domains share infrastructure.
- Explicit module boundaries if one site identity becomes large enough to split.
- Database/source boundaries that can move independently from the web application.

## Adding A New Mode

To add a real site mode:

1. Add the mode value to `SiteMode`.
2. Add host/domain mapping in `SiteModeOptions`.
3. Update `SiteModeMiddleware.ResolveSiteMode`.
4. Add route and static asset ownership rules in `SiteRouteOwnership`.
5. Add mode-owned views under `Views/SiteModes/{Mode}`.
6. Add separate branding component partials under `Views/SiteModes/{Mode}/Branding/`, or rely on the matching Unassigned component fallback.
7. Add a presentation module under `Services/Site/ModePresentation`.
8. Add mode-owned static assets under `wwwroot/site-modes/{mode}`.
9. Add the mode stylesheet to `SiteModeStylesheetResolver` when the mode owns a visual identity or tooling overlay.
10. Add `VisibleInModes` eligibility to content that belongs in the new mode.
11. Add a `ContentStorage:Modes:{Mode}` source override only when the new real site identity needs to differ from the global source list.

Do not add source override layers for Development or Unassigned. Development is manually selected; Unassigned is the
global fall-through.

## Practical Rule

If content is only for one site identity, make its mode eligibility explicit. If one content page belongs in multiple
listing contexts, give the same stable page multiple context tags rather than duplicating it. If a database source should
apply everywhere, put it in the ordered global list; use per-mode source differences only for real site identities that
actually need them.
