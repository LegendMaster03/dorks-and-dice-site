# Site Mode Architecture

This project uses a site mode architecture so one ASP.NET Core MVC codebase can serve multiple site identities from
different domains without duplicating the whole application.

The current modes are:

- `Professional`: Kyle Barnett professional resume, portfolio, articles, and professional-owned assets.
- `DorksAndDice`: group-facing Dorks & Dice site experience.
- `Development`: local-only preview mode used for testing shared routes, mode switching, and unlisted content.
- `Unassigned`: bare-bones fallback for domains that are connected to the app but not mapped to a mode.

## Goals

- Keep one deployable codebase while the site identities are still closely related.
- Make ownership explicit so professional pages, Dorks & Dice pages, and shared pages do not blend accidentally.
- Allow shared infrastructure for layout, routing, article handling, static files, and deployment.
- Preserve a future path to split one mode into a separate app if it grows enough to justify that.

## Request Flow

`SiteModeMiddleware` resolves the active mode for each request.

1. Normalize the request host.
2. Check the host against configured professional, Dorks & Dice, and development host lists.
3. On development hosts, read the selected preview mode from `DevelopmentPreviewSiteMode`.
4. Read the unlisted article preview flag from `DevelopmentIncludeUnlistedArticles` on development hosts only.
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

Shared article views remain under `Views/Articles` because articles are a shared surface with mode-aware eligibility.

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
dependent only on shared infrastructure.

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

## Articles

Article metadata currently lives in `ArticleCatalogService`.

Articles are currently hand-authored Razor pages. There is not yet a CMS, admin editor, or database-backed publishing
workflow, so each article currently needs explicit source changes to add the page, metadata, route, and supporting
assets. A better article creation workflow is a future goal, but the current implementation should stay simple until the
site has enough article volume to justify that work.

The likely future direction is a lightweight wiki-style authoring system inspired by MediaWiki workflows. That would let
articles be written as structured content instead of full Razor pages while preserving the application's existing
article metadata, mode visibility, listing, noindex, routing, and asset ownership rules.

The Dorks & Dice mode may eventually use the same content foundation for campaign knowledge management. That direction
would likely combine wiki-style pages with selected ideas from campaign planning tools and local linked-note workflows:
characters, locations, factions, session notes, timelines, cross-links, and private/public visibility boundaries.

Important flags:

- `Listed`: controls whether an article appears in normal indexes.
- `VisibleInModes`: allow-list of modes where the article is eligible.
- `PostedDateText`: displayed post date for listed article cards.

Unlisted articles:

- are hidden from normal article indexes
- remain accessible by direct URL when eligible for the current mode
- render `noindex, nofollow`
- show as `Unlisted` in development preview

`Development` mode ignores `VisibleInModes` for local inspection, but it does not automatically include unlisted
articles. The separate unlisted toggle controls that.

Mode eligibility is an internal content gate, not a user-facing filter. After the current mode has determined which
articles are eligible, the article index can expose normal user filters such as search, category, and tags.

## User-Facing Filters

User-facing filters operate only on content that has already passed the active mode's ownership and visibility rules.
They are a convenience layer for small curated indexes, not a replacement for mode access control or a full content
management system.

Current user-facing filters include:

- project tags on the Professional resume project list
- article search
- article categories
- article tags

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

The search syntax operates only after route ownership and mode visibility have already limited the available content.

This implementation should stay intentionally modest until the content volume justifies more. Do not add taxonomy
management, article editing tools, or exhaustive parser behavior simply because the syntax could support it later. For
now, tags should be added only when they make an existing project or article easier to find.

## Local Development Preview

Development hosts are configured in `SiteModeOptions`.

The development ribbon:

- shows the selected preview mode
- allows switching between Dorks & Dice, Professional, and Development
- allows showing unlisted articles
- applies changes immediately without an apply button
- stores mode and unlisted state in cookies
- shows route mismatch warnings when the selected mode could not access the current route on a real domain

The selected preview mode is the source of truth for branding, layout, navigation, mode-owned CSS, and content filtering
while on a development host. Development-tool CSS is added as an overlay after the selected mode stylesheet.

## Future Extraction Path

This architecture intentionally avoids premature separation, but it does not block future separation.

Because each mode already owns its pages, branding, asset paths, stylesheets, and access rules, a mode can later be
extracted into a separate application with clearer boundaries than a fully blended site would provide.

The current shape is therefore a middle ground:

- One deployment and one codebase while the domains share infrastructure.
- Explicit module boundaries if one site identity becomes large enough to split.

## Adding A New Mode

To add a mode:

1. Add the mode value to `SiteMode`.
2. Add host/domain mapping in `SiteModeOptions`.
3. Update `SiteModeMiddleware.ResolveSiteMode`.
4. Add route and static asset ownership rules in `SiteRouteOwnership`.
5. Add mode-owned views under `Views/SiteModes/{Mode}`.
6. Add separate branding component partials under `Views/SiteModes/{Mode}/Branding/`, or rely on the matching Unassigned component fallback.
7. Add a presentation module under `Services/Site/ModePresentation`.
8. Add mode-owned static assets under `wwwroot/site-modes/{mode}`.
9. Add the mode stylesheet to `SiteModeStylesheetResolver` when the mode owns a visual identity or tooling overlay.
10. Update article metadata where articles should be eligible in the new mode.

## Practical Rule

If content is only for one site identity, put it under that mode. If content is intentionally shared, keep it in a shared
surface and make the mode filtering explicit.
