#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/luna"
REGISTRY="ghcr.io"
IMAGE_PREFIX="ghcr.io/renanmoraisdasilva"
DEPLOYMENT_IMAGE="$IMAGE_PREFIX/luna-frontend:latest"
DEPLOYMENT_DIR="/opt/luna-deployment"

SERVICES=(sqlserver frontend identity catalog orders payments inventory shipping)

cd "$APP_DIR"

exec 9>"$APP_DIR/.update.lock"
flock -n 9 || { echo "Another Luna update is already running."; exit 0; }

if [[ -f "$APP_DIR/.env" ]]; then
  set -a
  source <(sed 's/\r$//' "$APP_DIR/.env")
  set +a
fi

cleanup_login() {
  if [[ -n "${GHCR_PAT:-}" ]]; then
    docker logout "$REGISTRY" >/dev/null 2>&1 || true
  fi
}
trap cleanup_login EXIT

if [[ -n "${GHCR_PAT:-}" ]]; then
  : "${GHCR_USER:?GHCR_USER must be set when GHCR_PAT is set}"
  printf '%s' "$GHCR_PAT" | docker login "$REGISTRY" --username "$GHCR_USER" --password-stdin
fi

sync_deployment_files() (
  container="luna-deployment-sync-$$"
  staging_dir="$(mktemp -d)"

  cleanup() {
    docker rm "$container" >/dev/null 2>&1 || true
    rm -rf "$staging_dir"
  }
  trap cleanup EXIT

  docker create --name "$container" "$DEPLOYMENT_IMAGE" >/dev/null
  docker cp "$container:$DEPLOYMENT_DIR/." "$staging_dir/"

  test -f "$staging_dir/docker-compose.prod.yml"
  test -f "$staging_dir/update.sh"

  cp "$staging_dir/docker-compose.prod.yml" "$APP_DIR/docker-compose.prod.yml"
  cp "$staging_dir/update.sh" "$APP_DIR/update.sh.new"
  chmod +x "$APP_DIR/update.sh.new"
  mv "$APP_DIR/update.sh.new" "$APP_DIR/update.sh"
)

echo "Pulling Luna deployment configuration..."
docker pull "$DEPLOYMENT_IMAGE"
LUNA_IMAGE_TAG="$(docker image inspect "$DEPLOYMENT_IMAGE" --format='{{index .Config.Labels "org.opencontainers.image.revision"}}')"
: "${LUNA_IMAGE_TAG:?The deployment image is missing org.opencontainers.image.revision}"
export LUNA_IMAGE_TAG
sync_deployment_files

declare -A previous_digests

for service in "${SERVICES[@]}"; do
  [[ "$service" == sqlserver ]] && continue
  image="$IMAGE_PREFIX/luna-$service:$LUNA_IMAGE_TAG"
  previous_digests["$service"]="$(docker image inspect "$image" --format='{{index .RepoDigests 0}}' 2>/dev/null || true)"
done

COMPOSE=(docker compose --env-file "$APP_DIR/.env" -p luna -f docker-compose.prod.yml)

repair_stale_network() {
  if docker network inspect luna_default >/dev/null 2>&1; then
    current_label="$(docker network inspect luna_default --format '{{index .Labels "com.docker.compose.network"}}' 2>/dev/null || true)"
    if [[ -z "$current_label" || "$current_label" != "luna" ]]; then
      echo "Removing stale Docker network luna_default (label: ${current_label:-<unset>})"
      docker network rm luna_default >/dev/null 2>&1 || true
    fi
  fi
}

echo "Reconciling existing Compose state..."
repair_stale_network

echo "Pulling Luna images..."
"${COMPOSE[@]}" pull

changed=false

for service in "${SERVICES[@]}"; do
  [[ "$service" == sqlserver ]] && continue
  image="$IMAGE_PREFIX/luna-$service:$LUNA_IMAGE_TAG"
  old_digest="${previous_digests[$service]}"
  new_digest="$(docker image inspect "$image" --format='{{index .RepoDigests 0}}' 2>/dev/null || true)"

  if [[ "$old_digest" != "$new_digest" ]]; then
    echo "$service changed"
    echo "  old: ${old_digest:-<not installed>}"
    echo "  new: ${new_digest:-<unknown>}"
    changed=true
  fi
done

if [[ "$changed" == true ]]; then
  echo "New image(s) detected. Updating Luna..."
else
  echo "No image changes detected; reconciling Compose configuration..."
fi

"${COMPOSE[@]}" up -d --wait --remove-orphans "${SERVICES[@]}"
echo "Luna deployment is up to date."

docker image prune -f >/dev/null
