# dorks-and-dice-site

One ASP.NET Core MVC application currently serves the **Dorks & Dice** site and the **Kyle Barnett professional** site. The codebase is being structured as a reusable multi-mode framework: normal site modes compose shared framework behavior, database-backed content, presentation, plugins, and Tools without duplicating the application.

## Stack

- .NET 10 / ASP.NET Core MVC / Razor Views
- Bootstrap
- Entity Framework Core
- SQLite and PostgreSQL content sources
- ASP.NET Core Identity
- Docker
- GitHub Actions on a self-hosted runner

## Architecture

The detailed contracts are documented under `docs/`, especially:

- `docs/site-mode-architecture.md`
- `docs/refactor-architecture-plan.md`
- `docs/mode-definition-decision.md`
- `docs/homepage-content-convergence.md`
- `docs/plugin-boundaries.md`
- `docs/tool-hosting-boundaries.md`

The important boundaries are:

- **Framework**: routing, mode resolution, content/revisions, media, page composition, identity/authorization, and Tool hosting.
- **Normal modes**: site identity and composition such as Professional and Dorks & Dice.
- **Trusted Preview**: synthetic development/control-plane context used to inspect normal modes. It is not a normal tenant mode.
- **Fallback**: framework-owned behavior for an unmapped host or unavailable presentation part. It is not a normal tenant mode.
- **Plugins**: small in-process executable capabilities exposed through stable component/presentation keys.
- **Tools**: substantial workflows or applications with their own lifecycle and possible separate/containerized runtime.

The final authoritative persistence model for normal mode definitions is intentionally still open. Generic framework code should depend on stable registered mode IDs and `ISiteModeRegistry`, not on a permanent assumption that modes must be defined in C# or a database.

## Content

Navigable pages use the unified revision-oriented content model. Projects, Experience records, Articles, and Homepages are contexts represented by tags rather than separate page stores.

Current revision storage includes page identity, immutable revisions, revision tags, revision-visible modes, managed media, page/media dependencies, revision media references, and redirects. Content sources are named and composable. Later sources override earlier records with the same stable identity or slug.

Authoring is split into two surfaces:

- **Editor**: normal mode-scoped authoring in the mode's configured workspace.
- **Development**: trusted cross-source inspection and authoring operations.

The Development database screen supports deliberate **single-page moves** between configured sources. A move refuses to overwrite an existing target page/history. The old one-time bulk move UI is not part of the permanent authoring surface.

## Managed media

Authored media is stored in the content database rather than requiring files under `wwwroot`.

Supported media types are:

- JPEG
- PNG
- WebP
- GIF
- passive SVG
- PDF

Uploads are size-limited, signature-validated, and SHA-256 hashed. SVG passes a passive-content validator and is served with a restrictive content security policy.

Managed media uses stable URLs:

```text
/content/media/{assetKey}/{fileName}
```

The media library can replace the bytes behind an existing asset while retaining the asset key, canonical filename, URL, attachments, and revision references. Replacement uses the normal upload validation boundary, requires the media type to remain unchanged, and treats identical bytes as a no-op. Public media responses use SHA-256 ETags with revalidation instead of immutable long-term caching so stable URLs can safely reflect replacements.

PDFs can be previewed inline in the media library and page dependency view, with an Open PDF fallback.

## Public text representations

The application exposes runtime text views of the active normal site mode:

- `/site.txt` — full current public text representation assembled from the same content catalog, source precedence, visibility, and listing rules as the web site.
- `/llms.txt` — compact discovery/index representation that links to `/site.txt` and the current public pages.

These endpoints are generated at request time from current database content. Development/synthetic state and unlisted content are not exported.

The former build-time `ResumeTxtGenerator` and static `kyle-resume.txt` are retired. The historical professional text-resume URL redirects to `/site.txt`.

## Site modes and domains

Normal modes currently include:

- `professional`
- `dorks-and-dice`

Configured professional domains include:

- `kylebarnett.com`
- `k-barnett.com`
- `kyle-barnett.com`
- `kylebarnett.net`
- `kylebarnett.org`
- `kylebarnett.dev`

Dorks & Dice currently uses:

- `dorks-and-dice.com`

Host/domain configuration is deployment-owned and is not part of portable authored content.

## Routes

Important public/shared routes include:

- `/` — mode-adaptive homepage
- `/articles` and `/articles/{slug}` — mode-aware content
- `/resume` and `/resume/{slug}` — Professional-owned resume/project/experience routes
- `/content/media/...` — visibility-checked managed media
- `/site.txt` — full public text representation
- `/llms.txt` — compact public text index
- `/health` — deployment health check

Route ownership is enforced by `Services/Site/SiteRouteOwnership.cs`. Real domains receive normal 404 behavior for routes outside the active mode. Trusted development hosts may inspect cross-mode routes with diagnostic warnings.

## Plugins

Current in-process plugins include:

- `professional-portfolio`
- `discord-widget`
- `minecraft-server-status`

Authored content may invoke installed plugin components by stable key but can not introduce executable code or deployment secrets.

Minecraft connection details remain deployment configuration. Authored Markdown can select the provided component but can not redirect the Minecraft status service to an arbitrary host.

## Tools

Tools are separate from plugins. The Tool hosting boundary supports substantial application/workflow capabilities with mode-aware exposure and a path toward containerized or separately hosted runtimes.

Campaign systems and future D&D applications belong on the Tool side of this boundary rather than being forced into the mode definition itself.

## Local development

From the repository root:

```bash
dotnet build dorks-and-dice-site.slnx -c Release
dotnet run --project dorks-and-dice-site
```

Configured development hosts use the trusted preview wrapper. Preview selection, unlisted-content inspection, and explicit content-source selection are stored in development-only cookies and are ignored on real domains.

## Validation

The validation workflow runs on every pushed branch and pull request to `main`. It starts disposable PostgreSQL databases and runs:

```bash
dotnet test dorks-and-dice-site.slnx --configuration Release
```

PostgreSQL-backed content and Identity integration tests receive isolated test connection strings from the workflow.

## Docker and deployment

Build from repository root:

```bash
docker build -t dorks-and-dice-site:latest -f dorks-and-dice-site/Dockerfile dorks-and-dice-site
```

Current production assumptions include:

- TrueNAS SCALE host
- self-hosted GitHub Actions runner labeled `dorks-and-dice-site`
- application container on port `8080`
- host mapping `8090:8080`
- reverse proxy in front of the application

Deployment hostnames, credentials, database connection strings, trusted-network configuration, and secrets are deployment-owned and must not be embedded in portable mode/plugin/content packages.

## Change guidance

- Preserve `/health` behavior for deployment checks.
- Keep generic framework code source- and mode-agnostic where possible.
- Use stable registered mode IDs at runtime boundaries.
- Keep Development/Trusted Preview authority separate from normal Editor authority.
- Do not treat the fallback framework state as a normal mode.
- Put editable authored content and authored media in the content system rather than C# or static files.
- Put visual identity/theme assets in presentation/theme ownership rather than the content database merely because they are images.
- Use plugins for small in-process executable components and Tools for substantial applications/workflows.
- Do not put deployment secrets, network endpoints, or credentials into authored content or portable mode definitions.
