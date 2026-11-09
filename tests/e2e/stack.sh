#!/usr/bin/env bash
# QA-04: the OPS-01 compose stack with the BE-16 demo data, isolated for E2E runs.
#   ./stack.sh up     fresh database (volumes removed first), build, start, wait until the API answers
#   ./stack.sh down   stop and remove containers and volumes
#   ./stack.sh logs   api and web logs (for a failed run)
# Its own compose project, database port and web port, so the dev stack and its data are left alone.
set -euo pipefail

cd "$(dirname "$0")/../.."

export COMPOSE_PROJECT_NAME="${E2E_PROJECT:-flowops-e2e}"
export FLOWOPS_WEB_PORT="${E2E_WEB_PORT:-9443}"
export POSTGRES_PORT="${E2E_DB_PORT:-55432}"
# Throwaway database inside a throwaway stack; not a secret (AGENTS.md: no real secrets in source).
export POSTGRES_PASSWORD="${E2E_DB_PASSWORD:-e2e-only-not-secret}"
export POSTGRES_DB=flowops POSTGRES_USER=flowops

compose() { docker compose -f docker-compose.yml -f tests/e2e/docker-compose.e2e.yml "$@"; }
base_url="https://localhost:${FLOWOPS_WEB_PORT}"

case "${1:-}" in
  up)
    compose down -v --remove-orphans >/dev/null 2>&1 || true
    compose up -d --build --wait db
    compose up -d --build
    echo "Waiting for ${base_url}/api/health (the demo data is seeded before the API serves)..."
    for _ in $(seq 1 120); do
      if curl -fsk "${base_url}/api/health" >/dev/null 2>&1; then
        echo "Stack ready at ${base_url}"
        exit 0
      fi
      sleep 2
    done
    echo "The stack did not become healthy in 4 minutes." >&2
    compose ps >&2
    compose logs --tail 80 api web >&2
    exit 1
    ;;
  down)
    compose down -v --remove-orphans
    ;;
  logs)
    compose logs --tail 200 api web
    ;;
  *)
    echo "usage: $0 up|down|logs" >&2
    exit 2
    ;;
esac
