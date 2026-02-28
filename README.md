# dorks-and-dice-site

Front-door website for **dorks-and-dice.com**, built as an **ASP.NET Core MVC (Razor Views)** app.

This repo contains:
- Public Dorks & Dice homepage
- Professional resume/profile section for Kyle Barnett
- Project and experience detail pages
- Health endpoint for deployment checks

## Stack

- .NET 10 (ASP.NET Core MVC + Razor Views)
- Bootstrap
- Docker (multi-stage build)
- GitHub Actions (self-hosted runner deployment)

## Repository Layout

- `dorks-and-dice-site/` - ASP.NET Core MVC project
- `dorks-and-dice-site/Dockerfile` - container build definition
- `.github/workflows/deploy.yml` - CI/CD workflow
- `dorks-and-dice-site.slnx` - solution file

## Local Development

From repository root:

```bash
dotnet build dorks-and-dice-site.slnx -c Release
dotnet run --project dorks-and-dice-site
```

App defaults to the MVC route:
- Home: `/`
- Resume: `/resume`
- Health check: `/health`

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
- PDF resume: `dorks-and-dice-site/wwwroot/files/kyle-resume.pdf`
- ATS text resume: `dorks-and-dice-site/wwwroot/files/kyle-resume.txt`

The text resume is auto-generated from `Views/Home/Resume.cshtml` during build by:
- `tools/ResumeTxtGenerator/`
- MSBuild target `GenerateResumeTextFile` in `dorks-and-dice-site.csproj`

## Notes for Future Changes

- Keep deployment port/proxy assumptions unchanged unless infrastructure is updated first.
- If changing Docker paths, update both local docs and `.github/workflows/deploy.yml`.
- Preserve `/health` behavior (`200 OK` with plain text `OK`) for smoke checks and monitoring.
