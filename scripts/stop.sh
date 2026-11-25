#!/usr/bin/env bash
# Stop all services (keeps data volumes).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
docker compose -f docker/docker-compose.yml down "$@"
echo "Services stopped."