#!/usr/bin/env bash
# Start all services.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
docker compose -f docker/docker-compose.yml up -d "$@"
echo "Services started. Frontend: http://localhost:3000"