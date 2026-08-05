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
- `/site-modes/unassigned/...` is Unassigned-owned.

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

## Branding

Each mode has one branding module:

```text
Views/SiteModes/{Mode}/_Branding.cshtml
```

The layout calls that module for the header and footer using `SiteModeBrandingPart`.

If a mode module is missing or throws `SiteModeBrandingPartUnavailableException` for a specific part, the layout falls
back to the matching part in `Views/SiteModes/Unassigned/_Branding.cshtml`.

This keeps branding modular by mode without creating separate files for every small branding function.

## Presentation Modules

Non-view presentation defaults live under:

```text
dorks-and-dice-site/Services/Site/ModePresentation/
```

Each mode can define:

- title suffixes
- default meta descriptions
- article index copy
- article filter visibility policy

`SiteModePresentationService` calls the active mode module and falls back to the same function on the Unassigned module
when a mode module is missing or explicitly reports that a presentation part is unavailable.

## Static Assets

Shared framework assets remain at root paths:

- `wwwroot/css`
- `wwwroot/js`
- `wwwroot/lib`
- `wwwroot/favicon.ico`
- `wwwroot/robots.txt`

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

## Articles

Article metadata currently lives in `ArticleCatalogService`.

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

## Local Development Preview

Development hosts are configured in `SiteModeOptions`.

The development ribbon:

- shows the selected preview mode
- allows switching between Dorks & Dice, Professional, and Development
- allows showing unlisted articles
- applies changes immediately without an apply button
- stores mode and unlisted state in cookies
- shows route mismatch warnings when the selected mode could not access the current route on a real domain

The selected preview mode is the source of truth for branding, layout, navigation, and content filtering while on a
development host.

## Future Extraction Path

This architecture intentionally avoids premature separation, but it does not block future separation.

Because each mode already owns its pages, branding, asset paths, and access rules, a mode can later be extracted into a
separate application with clearer boundaries than a fully blended site would provide.

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
6. Add a `_Branding.cshtml` module or rely on Unassigned fallback.
7. Add a presentation module under `Services/Site/ModePresentation`.
8. Add mode-owned static assets under `wwwroot/site-modes/{mode}`.
9. Update article metadata where articles should be eligible in the new mode.

## Practical Rule

If content is only for one site identity, put it under that mode. If content is intentionally shared, keep it in a shared
surface and make the mode filtering explicit.
