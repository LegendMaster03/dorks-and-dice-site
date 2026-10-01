# Private Tool tunnels

This document is the canonical operational runbook for Dorks & Dice private Tool-to-Tool tunnels.

Private tunnels are a deployment and authentication mechanism for a source Tool that must call a target Tool's private API without exposing that private API to the shared Tool backend network. They are deliberately separate from ordinary `DelegationTargets`.

Application-specific repositories may add their own deployment details, but they should refer back to this document for the generic tunnel model and creation procedure.

## Security model

A private tunnel has four independent controls:

1. **Site authorization** — Site explicitly allowlists one source registration key to one target registration key.
2. **Pair-specific network reachability** — only the source and the target's private ingress join the source/target data network.
3. **Target provenance** — Site issues a target-scoped Tool ticket whose authentication context records the immediate private source in `PrivateTunnelSourceToolKey` / `PrivateTunnelSourceToolSlug`.
4. **Target API boundary** — the target private ingress must still reject APIs that do not belong on the private surface and must continue applying normal user/domain authorization.

None of these controls replaces the others.

The core invariants are:

- `DelegationTargets` does not grant private API access.
- private-tunnel configuration does not grant ordinary Site-proxied delegation.
- private access is pair-specific and non-transitive.
- Site is the identity and authorization **control plane**, not the private HTTP data plane.
- the source sends the actual private API request directly to the target private ingress.
- browsers never receive the private target URL, private-tunnel capability, target Tool ticket, or private authentication headers.
- a target private ingress must not be attached to the general `dorks-and-dice-backend` merely to obtain unrelated dependencies such as database access.

## Terminology

- **source** — the Tool that needs the private API, for example `rules-wiki`.
- **target** — the Tool whose private API is being consumed, for example `rules-core`.
- **pair network** — the private data-plane network containing only the source and the target private ingress.
- **control-plane network** — the restricted network through which the target private ingress can redeem Site-issued Tool tickets.
- **dependency network** — a restricted network used when the target private ingress needs a backend dependency such as PostgreSQL without joining the general Tool backend.

A deployment may need more than one dependency network. Dependency networks are not part of the private-tunnel authorization protocol; they are deployment isolation controls.

## Registration prerequisites

Both source and target must have stable Tool registration keys in Site.

A user-facing source normally has `Kind: Application` and a public slug. A backend-only target may use `Kind: Service` with no public slug. Stable registration keys, not display names or historical slugs, are the security identities used by the tunnel policy.

The target must be enabled and available in the Site mode carried by the source authentication context.

## 1. Grant the source/target relationship in Site

Private tunnels are deployment configuration, not mutable Tool registry data.

Configure:

```text
ToolHosting:PrivateTunnels:{sourceToolKey}:{index} = {targetToolKey}
```

ASP.NET Core environment-variable form:

```text
ToolHosting__PrivateTunnels__{sourceToolKey}__{index}={targetToolKey}
```

Example:

```text
ToolHosting__PrivateTunnels__rules-wiki__0=rules-core
```

Because Tool keys may contain hyphens, do not rely on interactive shell `export` syntax for these names. Put the key directly in Compose YAML or another deployment configuration source.

Example Site Compose override:

```yaml
services:
  site:
    environment:
      ToolHosting__PrivateTunnels__rules-wiki__0: "rules-core"
    networks:
      - default
      - backend
      - control-plane

networks:
  control-plane:
    external: true
    name: dorks-and-dice-tool-control-plane
```

Changing Docker network membership alone does **not** grant the tunnel. The Site allowlist is authoritative for source-to-target permission.

## 2. Create the external networks

Create one pair network per private source/target relationship and a restricted Site control-plane network if it does not already exist.

Example:

```bash
docker network inspect dorks-and-dice-private-rules-wiki-rules-core >/dev/null 2>&1 \
  || docker network create dorks-and-dice-private-rules-wiki-rules-core

docker network inspect dorks-and-dice-tool-control-plane >/dev/null 2>&1 \
  || docker network create dorks-and-dice-tool-control-plane
```

The intended membership is:

```text
pair network
  source Tool
  target private ingress

control-plane network
  Site
  target private ingress
```

Ordinary Tools must join neither network unless they are explicitly one of the peers for another independently configured tunnel.

## 3. Give the target private ingress isolated dependency access

A private ingress often needs dependencies that already live on `dorks-and-dice-backend`. Do not solve that by attaching the private ingress to the shared backend network.

Instead, create a restricted dependency network for the minimum required peers.

For a PostgreSQL-backed target:

```bash
docker network inspect dorks-and-dice-rules-core-db >/dev/null 2>&1 \
  || docker network create dorks-and-dice-rules-core-db
```

The intended membership is:

```text
database dependency network
  target private ingress
  PostgreSQL
```

The shared PostgreSQL process may still remain attached to its existing backend network for all existing consumers. Adding a second Docker network does not move the container or remove its original network attachment.

### Platform-managed dependencies

If PostgreSQL or another dependency is managed by TrueNAS or another external platform, a one-time command such as `docker network connect` is only a runtime attachment. It can disappear when the platform recreates that container.

Persist the additional network in the platform-managed deployment configuration when that platform supports it. If the platform can not express the attachment, install an idempotent host startup/init task that checks the network and reconnects the dependency. Do not treat a one-time interactive `docker network connect` as durable production configuration.

Repository-controlled target containers should declare their dependency network in Compose so their side of the attachment is recreated automatically.

## 4. Configure the target private ingress

A target should expose private APIs through a private-only ingress rather than through the same network surface used by ordinary Tools.

The public and private ingress may run the same image and share the same database, but they must enforce separate API surfaces where the target supports that distinction.

Conceptually:

```text
shared backend --------> target public ingress    PublicOnly

source/target network -> target private ingress   PrivateOnly
                             |
                             +--> control-plane network --> Site
                             |
                             +--> restricted dependency network --> database
```

The target private base URL is deployment configuration owned by the source. Site does not return a target URL during ticket exchange.

## 5. Configure the source

The source backend must know the private target base URL and join the pair network.

It must not expose that URL or tunnel credentials to browser code.

The source performs private operations by named application behavior and server-side routing. It must not create a browser-controlled arbitrary proxy to the target private API.

## 6. Authentication and request flow

The control-plane flow is:

1. The source redeems its normal Site Tool ticket.
2. If Site sees at least one configured private target for that source, Site includes a short-lived server-only private-tunnel capability on the introspection response.
3. The source exchanges that capability for a fresh target-scoped ticket:

   ```text
   POST /tool-host/{sourceSlug}/api/private-tunnel/{targetKey}/ticket
   ```

   A headless source uses:

   ```text
   POST /tool-host/registrations/{sourceKey}/api/private-tunnel/{targetKey}/ticket
   ```

4. Site verifies the source capability and the deployment allowlist.
5. Site creates target-specific authorization context carrying private-tunnel provenance.
6. Site returns the target Tool ticket and the target's stable introspection path.
7. The source calls the target private ingress directly over the pair network using the target ticket and introspection path.
8. The target redeems that ticket through Site and applies its private API boundary plus normal domain authorization.

Private target tickets use stable key-scoped introspection such as:

```text
/tool-host/registrations/rules-core/api/introspect
```

### Internal Site hostname

Server-to-server control-plane calls commonly use an internal hostname such as `dorks-and-dice-site`. That hostname may not map to a public Site mode. Site therefore treats exact Tool Host introspection, ordinary delegation, and private-tunnel ticket-exchange routes as shared system routes so `SiteModeMiddleware` does not rewrite them to the framework fallback 404 page.

Adding a new Tool Host control-plane route requires corresponding route-ownership coverage and an integration test through the internal Site hostname.

## 7. Make deployment overlays durable

If a repository uses a Compose overlay to add the private ingress or pair-network attachment, every production deployment must use that overlay.

For example:

```bash
docker compose \
  --project-name <project> \
  --env-file <deployment-env> \
  -f docker-compose.yml \
  -f docker-compose.private-tunnel.yml \
  config

docker compose \
  --project-name <project> \
  --env-file <deployment-env> \
  -f docker-compose.yml \
  -f docker-compose.private-tunnel.yml \
  up -d --force-recreate --remove-orphans
```

A CI/CD workflow that deploys only `docker-compose.yml` will silently remove overlay-only services or network attachments during the next recreation. Overlay usage belongs in the deployment workflow, not in an operator's memory.

## 8. Verification checklist

Verify the grant is present in Site:

```bash
docker inspect dorks-and-dice-site \
  --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | grep 'ToolHosting__PrivateTunnels'
```

Verify pair-network membership:

```bash
docker network inspect <pair-network> \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | sort
```

It should contain only the intended source and target private ingress.

Verify control-plane membership:

```bash
docker network inspect <control-plane-network> \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | sort
```

It should contain Site and the target private ingress, plus only other explicitly intended control-plane peers.

Verify dependency-network membership separately. A private target should not appear on `dorks-and-dice-backend` merely for dependency access.

Verify both target surfaces independently:

- public ingress is reachable from the normal backend and reports ready;
- private ingress is reachable from the pair network and reports ready;
- private API calls through the public ingress fail closed;
- public consumer API calls through the private ingress fail closed when the target supports split API surfaces.

Finally, exercise one real source operation end-to-end. Readiness alone proves network/process health, not ticket exchange or application behavior.

## 9. Revocation

To revoke a private relationship:

1. remove the `ToolHosting:PrivateTunnels` source/target entry from Site and redeploy Site;
2. verify the source no longer receives usable private-tunnel authorization for that target;
3. remove the source from the pair network if the relationship is retired rather than temporarily disabled;
4. remove any target-specific private ingress or dependency attachment that is no longer needed.

Remove authorization before dismantling networking. That makes revocation fail closed even while old containers or networks are being cleaned up.

## Current production example: Rules Wiki -> Rules Core

The current Dorks & Dice deployment uses:

```text
source key:        rules-wiki
target key:        rules-core
pair network:      dorks-and-dice-private-rules-wiki-rules-core
control network:   dorks-and-dice-tool-control-plane
database network:  dorks-and-dice-rules-core-db
private target:    http://dorks-and-dice-rules-core-private:8080
```

Site authorization:

```text
ToolHosting__PrivateTunnels__rules-wiki__0=rules-core
```

Rules Core deployment configuration:

```text
RulesCorePrivate__Network=dorks-and-dice-private-rules-wiki-rules-core
RulesCorePrivate__ToolHostBaseUrl=http://dorks-and-dice-site:8080
ToolHosting__ControlPlaneNetwork=dorks-and-dice-tool-control-plane
RulesCorePrivate__DatabaseNetwork=dorks-and-dice-rules-core-db
```

Rules Wiki deployment configuration:

```text
RulesCorePrivate__Network=dorks-and-dice-private-rules-wiki-rules-core
RULES_CORE_PRIVATE_BASE_URL=http://dorks-and-dice-rules-core-private:8080
```

Expected network membership:

```text
dorks-and-dice-private-rules-wiki-rules-core
  dorks-and-dice-rules-core-private
  dorks-and-dice-rules-wiki

dorks-and-dice-tool-control-plane
  dorks-and-dice-rules-core-private
  dorks-and-dice-site

dorks-and-dice-rules-core-db
  dorks-and-dice-rules-core-private
  PostgreSQL
```

The Rules Core and Rules Wiki repositories own their Compose overlay attachments. The PostgreSQL peer is managed by TrueNAS and its additional database-network attachment must therefore be persisted on the TrueNAS side rather than being assumed durable after an interactive `docker network connect`.
