#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/luna"
REGISTRY="ghcr.io"
IMAGE_PREFIX="ghcr.io/renanmoraisdasilva"
DEPLOYMENT_IMAGE="$IMAGE_PREFIX/luna-frontend:latest"
DEPLOYMENT_DIR="/opt/luna-deployment"
RELEASES_DIR="$APP_DIR/releases"
CURRENT_LINK="$APP_DIR/current"
UPDATER_LINK="$APP_DIR/update.sh"
KEEP_RELEASES=3

cd "$APP_DIR"
mkdir -p "$RELEASES_DIR"

exec 9>"$APP_DIR/.update.lock"
flock -n 9 || { echo "Another Luna update is already running."; exit 0; }

[[ -f "$APP_DIR/.env" ]] || { echo "Missing $APP_DIR/.env" >&2; exit 1; }

env_value() {
  local key="$1"
  awk -v key="$key" '
    index($0, key "=") == 1 {
      value = substr($0, length(key) + 2)
      sub(/\r$/, "", value)
      if (value ~ /^".*"$/ || value ~ /^'"'"'.*'"'"'$/) {
        value = substr(value, 2, length(value) - 2)
      }
      print value
      exit
    }
  ' "$APP_DIR/.env"
}

GHCR_USER="${GHCR_USER:-$(env_value GHCR_USER)}"
GHCR_PAT="${GHCR_PAT:-$(env_value GHCR_PAT)}"

previous_release="$(readlink -f "$CURRENT_LINK" 2>/dev/null || true)"
release_dir=""
deployment_started=false
deployment_succeeded=false

cleanup_login() {
  if [[ -n "${GHCR_PAT:-}" ]]; then
    docker logout "$REGISTRY" >/dev/null 2>&1 || true
  fi
}

rollback_or_cleanup() {
  local status=$?

  if [[ "$deployment_started" == true && "$deployment_succeeded" != true && -n "$previous_release" ]]; then
    echo "Deployment failed; restoring $previous_release..." >&2
    previous_compose=(
      docker compose
      --project-name luna
      --env-file "$APP_DIR/.env"
      -f "$previous_release/docker-compose.prod.yml"
    )
    "${previous_compose[@]}" up -d --wait --remove-orphans || {
      echo "Rollback failed; inspect the Luna Compose project immediately." >&2
    }
  fi

  cleanup_login
  exit "$status"
}
trap rollback_or_cleanup EXIT

if [[ -n "${GHCR_PAT:-}" ]]; then
  : "${GHCR_USER:?GHCR_USER must be set when GHCR_PAT is set}"
  printf '%s' "$GHCR_PAT" | docker login "$REGISTRY" --username "$GHCR_USER" --password-stdin
fi

extract_release() (
  local revision="$1"
  container="luna-deployment-sync-$$"
  release_dir="$RELEASES_DIR/$revision"
  staging_dir="$(mktemp -d "$RELEASES_DIR/.staging.XXXXXX")"

  cleanup() {
    docker rm "$container" >/dev/null 2>&1 || true
    [[ -z "$staging_dir" ]] || rm -rf -- "$staging_dir"
  }
  trap cleanup EXIT

  if [[ -d "$release_dir" ]]; then
    test -f "$release_dir/docker-compose.prod.yml"
    test -f "$release_dir/update.sh"
    printf '%s\n' "$release_dir"
    exit 0
  fi

  docker create --name "$container" "$DEPLOYMENT_IMAGE" >/dev/null
  docker cp "$container:$DEPLOYMENT_DIR/." "$staging_dir/"

  test -f "$staging_dir/docker-compose.prod.yml"
  test -f "$staging_dir/update.sh"
  bash -n "$staging_dir/update.sh"
  mv "$staging_dir" "$release_dir"
  staging_dir=""
  printf '%s\n' "$release_dir"
)

echo "Pulling Luna deployment configuration..."
docker pull "$DEPLOYMENT_IMAGE"
LUNA_IMAGE_TAG="$(docker image inspect "$DEPLOYMENT_IMAGE" --format='{{index .Config.Labels "org.opencontainers.image.revision"}}')"
if [[ ! "$LUNA_IMAGE_TAG" =~ ^[0-9a-fA-F]{7,64}$ ]]; then
  echo "The deployment image is missing a valid Git revision label." >&2
  exit 1
fi
export LUNA_IMAGE_TAG

release_dir="$(extract_release "$LUNA_IMAGE_TAG")"

COMPOSE=(
  docker compose
  --project-name luna
  --env-file "$APP_DIR/.env"
  -f "$release_dir/docker-compose.prod.yml"
)

echo "Validating release $LUNA_IMAGE_TAG..."
"${COMPOSE[@]}" config --quiet
services="$("${COMPOSE[@]}" config --services)"
for service in frontend; do
  grep -qx "$service" <<<"$services" || {
    echo "Release is missing required service: $service" >&2
    exit 1
  }
done

echo "Pulling Luna images..."
"${COMPOSE[@]}" pull

echo "Starting Luna release $LUNA_IMAGE_TAG..."
deployment_started=true
"${COMPOSE[@]}" up -d --wait --remove-orphans

echo "Running deployment smoke checks..."
curl --fail --silent --show-error http://127.0.0.1:3000/api/health >/dev/null

temporary_link="$APP_DIR/.current.$$"
temporary_updater="$APP_DIR/.update.sh.$$"
ln -s "$CURRENT_LINK/update.sh" "$temporary_updater"
mv -Tf "$temporary_updater" "$UPDATER_LINK"
ln -s "$release_dir" "$temporary_link"
mv -Tf "$temporary_link" "$CURRENT_LINK"
deployment_succeeded=true

echo "Luna deployment is up to date."

current_release="$(readlink -f "$CURRENT_LINK")"
find "$RELEASES_DIR" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' \
  | sort -nr \
  | awk -v keep="$KEEP_RELEASES" 'NR > keep { sub(/^[^ ]+ /, ""); print }' \
  | while IFS= read -r old_release; do
      [[ "$old_release" == "$current_release" ]] || rm -rf -- "$old_release"
    done
