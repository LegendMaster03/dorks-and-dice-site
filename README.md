# dorks-and-dice-site

Single codebase for the **Dorks & Dice** group site and **Kyle Barnett** professional site, built as an
**ASP.NET Core MVC (Razor Views)** app.

This repo contains:
- Mode-aware Dorks & Dice and professional homepages
- Professional resume/profile section for Kyle Barnett
- Project and experience detail pages
- Long-form articles with mode-aware visibility
- Localhost development preview controls
- Health endpoint for deployment checks

## Stack

- .NET 10 (ASP.NET Core MVC + Razor Views)
- Bootstrap
- Docker (multi-stage build)
- GitHub Actions (self-hosted runner deployment)

## Repository Layout

- `dorks-and-dice-site/` - ASP.NET Core MVC project
- `dorks-and-dice-site/Dockerfile` - container build definition
- `dorks-and-dice-site/Services/Site/` - site mode, domain, dev preview, and route ownership logic
- `dorks-and-dice-site/Services/Articles/` - article catalog and article index filtering
- `dorks-and-dice-site/Services/Resume/` - resume content loading, validation, and generated text resume support
- `dorks-and-dice-site/Views/SiteModes/` - mode-owned homepage, resume, and mode-specific views
- `dorks-and-dice-site/Views/Articles/` - article index and article detail views
- `.github/workflows/deploy.yml` - CI/CD workflow
- `dorks-and-dice-site.slnx` - solution file

## Site Modes

The site uses explicit modes so one codebase can serve multiple domains while sharing content where appropriate.

- `Professional` mode serves Kyle Barnett professional domains.
- `DorksAndDice` mode serves `dorks-and-dice.com`.
- Local development hosts run a development preview wrapper that can emulate either mode or use `Development` mode.

Professional domains are configured in `Services/Site/SiteModeOptions.cs`:

- `kylebarnett.com`
- `k-barnett.com`
- `kyle-barnett.com`
- `kylebarnett.net`
- `kylebarnett.org`
- `kylebarnett.dev`

Dorks & Dice domains are also configured in `Services/Site/SiteModeOptions.cs`:

- `dorks-and-dice.com`

### Route Ownership

Route ownership is defined in `Services/Site/SiteRouteOwnership.cs`.

- `/` is mode-adaptive. It renders the professional homepage in `Professional` mode and the Dorks & Dice homepage in `DorksAndDice` mode.
- `/articles` and `/articles/...` are mode-adaptive.
- `/resume` and `/resume/...` are `Professional`-owned.
- Static assets, `/health`, and shared error routes are mode-neutral exceptions.

Assigned domains return the normal 404 page when a user requests a route that is not available in the current mode.
Localhost does not redirect; it shows a development ribbon warning so impossible states can be inspected.

### Mode-Owned Views

Mode homepage views live under `Views/SiteModes/`:

- `Views/SiteModes/Professional/Home.cshtml`
- `Views/SiteModes/Professional/Resume/` for professional resume partials, project details, and experience details
- `Views/SiteModes/DorksAndDice/Home.cshtml`

Mode branding modules also live under `Views/SiteModes/`:

- `Views/SiteModes/{Mode}/_Branding.cshtml`

`Views/Shared/_Layout.cshtml` calls the active mode's branding module once for the header and once for the footer. If a
mode does not provide the branding module, or that module reports that a requested branding part is unavailable, the
layout calls the same part from `Views/SiteModes/Unassigned/_Branding.cshtml`. This keeps one branding file per mode
while allowing individual branding functions to fall through independently.

Development preview tooling lives in `Views/SiteModes/Development/_DevelopmentTools.cshtml`. It is intentionally
Development-owned and does not fall back through `Unassigned`.

Mode presentation modules live under `Services/Site/ModePresentation/`. Each mode has one module for non-view
presentation defaults such as title suffixes, default meta descriptions, article index copy, and article index filter
visibility policy. The
`SiteModePresentationService` calls the active mode module and falls back to the matching function on the Unassigned
module when a mode module is missing or reports that a presentation part is unavailable.

`/Resume` remains as an alias route for the professional homepage content, but it is not shown in the shared ribbon.

## Article Visibility

Article metadata is currently defined in `Services/Articles/ArticleCatalogService.cs`.

Important article flags:

- `Listed`: controls normal article index visibility.
- `VisibleInModes`: allow-list of site modes where the article is eligible.

Articles are not eligible in any mode unless their metadata explicitly includes that mode in `VisibleInModes`.
`Development` mode is the local-preview exception: it ignores `VisibleInModes` so shared spaces such as `/articles`
can show content from every mode while testing.

Unlisted articles:

- remain accessible by direct URL when eligible for the current mode
- are hidden from normal article indexes
- render `noindex, nofollow`
- show `Unlisted` in development preview instead of `Draft`

Listed articles should use their configured post date in `PostedDateText`.

## Local Development

From repository root:

```bash
dotnet build dorks-and-dice-site.slnx -c Release
dotnet run --project dorks-and-dice-site
```

App defaults to the MVC route:
- Mode homepage: `/`
- Resume: `/resume`
- Articles: `/articles`
- Health check: `/health`

### Localhost Development Preview

Development hosts show a ribbon for previewing site modes and hidden content:

- `localhost`
- `127.0.0.1`
- `::1`
- `10.0.0.7`

The development ribbon posts to `/development-preview`, stores the selected mode in the
`DevelopmentPreviewSiteMode` cookie, and stores unlisted article visibility in the
`DevelopmentIncludeUnlistedArticles` cookie. Mode selection is independent of normal navigation, so reloading or
using browser back does not change the selected preview mode.

Development preview cookies are honored only on configured development hosts. Real domains ignore them.

The `Development` selection is the default on configured development hosts. It is a local-only view mode, useful for
shared routes such as `/articles` because it shows articles from all modes. It does not automatically include unlisted
articles; use the separate unlisted toggle when reviewing unpublished content.

The root route has separate Dorks & Dice and professional homepage implementations. In `Development` mode, `/` returns
404 with a route-resolution warning instead of silently choosing one of those mode-owned homepages.
The route-resolution page keeps the unresolved URL as the development preview return target, so changing modes from
the ribbon reloads the same path in the selected mode.

In local development preview, the selected ribbon mode is the source of truth for layout, branding, navigation, and
content filtering. `Development` mode can inspect routes across modes, but the chrome remains Development-mode chrome.
Shared routes need explicit Development-mode handling; otherwise they should return 404 rather than falling through to
one mode's implementation.

Hosts that are not configured in `SiteModeOptions` use the bare-bones `Unassigned` mode. Its homepage explains that
the domain is connected to the application but has not been assigned to a site mode.

If a development host is previewing a route that is not owned by the selected mode, the page still renders but the development ribbon shows a warning. Real domains redirect to `/` instead.

## Docker Build

The project uses a nested build context and Dockerfile path. Build from repo root:

```bash
docker build -t dorks-and-dice-site:latest -f dorks-and-dice-site/Dockerfile dorks-and-dice-site
```

This command must stay aligned with workflow/deployment expectations.

## Production Deployment

Deployment target:
- TrueNAS SCALE host
- GitHub Actions self-hosted runner user: `deploy`
- Runner label: `dorks-and-dice-site`

Reverse proxy:
- Nginx Proxy Manager
- `dorks-and-dice.com` and `www.dorks-and-dice.com` -> `10.0.0.7:8090` (HTTP upstream)
- Kyle Barnett professional domains should point to the same upstream and are classified by host name in `SiteModeOptions`.

Container runtime assumptions:
- Compose file path: `/mnt/HDDs/www/dorks-and-dice-site/compose.yml`
- Image: `dorks-and-dice-site:latest`
- Container name: `dorks-and-dice-site`
- Port mapping: `8090:8080`
- `ASPNETCORE_URLS=http://0.0.0.0:8080`

## CI/CD Workflow Summary

On push to `main`, workflow:
1. Builds Docker image using the nested Dockerfile command.
2. Runs a smoke test container and validates `GET /health` returns `OK`.
3. Cleans up the smoke test container.
4. Runs `docker compose up -d --force-recreate` in deployment directory.

## Resume and Profile Assets

Key static assets:
- PDF resume: `dorks-and-dice-site/wwwroot/site-modes/professional/files/kyle-resume.pdf`
- ATS text resume: `dorks-and-dice-site/wwwroot/site-modes/professional/files/kyle-resume.txt`

The text resume is auto-generated from `Content/Resume/resume.json` during build by:
- `dorks-and-dice-site/tools/ResumeTxtGenerator/`
- MSBuild target `GenerateResumeTextFile` in `dorks-and-dice-site.csproj`

## Notes for Future Changes

- Keep deployment port/proxy assumptions unchanged unless infrastructure is updated first.
- If changing Docker paths, update both local docs and `.github/workflows/deploy.yml`.
- Preserve `/health` behavior (`200 OK` with plain text `OK`) for smoke checks and monitoring.
- Keep route ownership changes in `Services/Site/SiteRouteOwnership.cs`.
- Keep domain classification changes in `Services/Site/SiteModeOptions.cs`.
- Prefer adding article visibility through article metadata flags instead of hardcoding route exceptions.
