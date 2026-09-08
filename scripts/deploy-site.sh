#!/usr/bin/env bash
set -euo pipefail

DEPLOY_DIR="${DORKS_SITE_DEPLOY_DIR:-/mnt/HDDs/www/dorks-and-dice-site}"
RUNTIME_DIR="${DORKS_SITE_RUNTIME_DIR:-$DEPLOY_DIR/.runtime/tool-hosting}"
CONTAINER_NAME="${DORKS_SITE_CONTAINER_NAME:-dorks-and-dice-site}"

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

if [ ! -d "$DEPLOY_DIR" ]; then
  fail "Deployment directory does not exist: $DEPLOY_DIR"
fi

cd "$DEPLOY_DIR"

if [ -n "${DORKS_SITE_COMPOSE_FILE:-}" ]; then
  BASE_COMPOSE="$DORKS_SITE_COMPOSE_FILE"
  [ -f "$BASE_COMPOSE" ] || fail "Configured Compose file does not exist: $BASE_COMPOSE"
else
  BASE_COMPOSE=""
  for candidate in compose.yaml compose.yml docker-compose.yaml docker-compose.yml; do
    if [ -f "$candidate" ]; then
      BASE_COMPOSE="$candidate"
      break
    fi
  done
  [ -n "$BASE_COMPOSE" ] || fail "No Compose file found in $DEPLOY_DIR"
fi

mkdir -p "$RUNTIME_DIR"
chmod 700 "$RUNTIME_DIR"

migrate_legacy_file() {
  local source_path="$1"
  local target_path="$2"

  if [ -f "$target_path" ]; then
    return
  fi

  if ! docker inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
    return
  fi

  if ! docker exec "$CONTAINER_NAME" test -f "$source_path" >/dev/null 2>&1; then
    return
  fi

  local migration_dir
  migration_dir="$(mktemp -d "$RUNTIME_DIR/.migration.XXXXXX")"
  if docker cp "$CONTAINER_NAME:$source_path" "$migration_dir/value"; then
    mv "$migration_dir/value" "$target_path"
    chmod 600 "$target_path"
    echo "Migrated legacy runtime file: $source_path"
  fi
  rmdir "$migration_dir" 2>/dev/null || true
}

# Before the first persistent deployment, preserve any runtime state that still exists in
# the current container. Missing files are expected when no Tools/campaigns have been saved.
migrate_legacy_file "/app/Content/tool-registry.json" "$RUNTIME_DIR/tool-registry.json"
migrate_legacy_file "/app/Content/campaign-access.json" "$RUNTIME_DIR/campaign-access.json"

SERVICE="${DORKS_SITE_COMPOSE_SERVICE:-}"
if [ -z "$SERVICE" ] && docker inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
  SERVICE="$(docker inspect -f '{{ index .Config.Labels "com.docker.compose.service" }}' "$CONTAINER_NAME" 2>/dev/null || true)"
fi

if [ -z "$SERVICE" ]; then
  mapfile -t SERVICES < <(docker compose -f "$BASE_COMPOSE" config --services)
  if [ "${#SERVICES[@]}" -eq 1 ]; then
    SERVICE="${SERVICES[0]}"
  else
    printf 'Compose services found:' >&2
    printf ' %s' "${SERVICES[@]}" >&2
    printf '\n' >&2
    fail "Could not determine the site service. Set DORKS_SITE_COMPOSE_SERVICE explicitly."
  fi
fi

OVERRIDE_FILE="$(mktemp /tmp/dorks-and-dice-runtime-compose.XXXXXX.yml)"
cleanup() {
  rm -f "$OVERRIDE_FILE"
}
trap cleanup EXIT

cat >"$OVERRIDE_FILE" <<EOF
services:
  $SERVICE:
    environment:
      ToolHosting__RegistryPath: /app/RuntimeData/tool-registry.json
      CampaignStorage__Path: /app/RuntimeData/campaign-access.json
    volumes:
      - type: bind
        source: $RUNTIME_DIR
        target: /app/RuntimeData
EOF

docker compose -f "$BASE_COMPOSE" -f "$OVERRIDE_FILE" config >/dev/null

echo "Deploying $SERVICE with persistent Tool Host runtime storage at $RUNTIME_DIR"
docker compose -f "$BASE_COMPOSE" -f "$OVERRIDE_FILE" up -d --force-recreate

EXPECTED_RUNTIME_DIR="$(readlink -f "$RUNTIME_DIR")"
ACTUAL_RUNTIME_DIR="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/app/RuntimeData"}}{{.Source}}{{end}}{{end}}' "$CONTAINER_NAME")"
[ "$ACTUAL_RUNTIME_DIR" = "$EXPECTED_RUNTIME_DIR" ] \
  || fail "Runtime-data mount is not active on $CONTAINER_NAME. Expected $EXPECTED_RUNTIME_DIR, found ${ACTUAL_RUNTIME_DIR:-<none>}."

docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$CONTAINER_NAME" \
  | grep -Fx 'ToolHosting__RegistryPath=/app/RuntimeData/tool-registry.json' >/dev/null \
  || fail "ToolHosting__RegistryPath is not configured for persistent storage."

docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$CONTAINER_NAME" \
  | grep -Fx 'CampaignStorage__Path=/app/RuntimeData/campaign-access.json' >/dev/null \
  || fail "CampaignStorage__Path is not configured for persistent storage."

healthy=false
for i in {1..20}; do
  body="$(docker run --rm \
    --network dorks-and-dice-backend \
    curlimages/curl:latest \
    -fsS "http://$CONTAINER_NAME:8080/health" 2>/dev/null || true)"
  if [ "$body" = "OK" ]; then
    healthy=true
    break
  fi
  sleep 1
done

if [ "$healthy" != "true" ]; then
  echo "Deployed site did not become healthy. Container logs:" >&2
  docker logs "$CONTAINER_NAME" >&2 || true
  exit 1
fi

echo "Deployment healthy; Tool Host runtime storage is persistent."
