#!/usr/bin/env bash
# Offline verification: no downloads, no restores, no installs.
# Checks configuration, Python syntax, and expected source files.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

FAIL=0
check() {
  local name="$1"; shift
  echo "==> $name"
  if "$@"; then echo "OK: $name"; else echo "FAILED: $name"; FAIL=1; fi
}

check "Docker Compose config" docker compose -f docker/docker-compose.yml config --quiet
check "Python syntax" python3 -m compileall -q src/NexaAI.AI/app

echo "==> Expected source files"
for f in \
  src/NexaAI.Api/Program.cs \
  src/NexaAI.Core/Infrastructure/Persistence/AppDbContext.cs \
  src/NexaAI.AI/app/main.py \
  src/NexaAI.Web/src/App.tsx \
  database/init/001_init.sql \
  .env.example \
  README.md \
  ARCHITECTURE.md; do
  if [ -f "$f" ]; then echo "  found $f"; else echo "  MISSING $f"; FAIL=1; fi
done

if [ "$FAIL" -eq 0 ]; then
  echo "ALL OFFLINE CHECKS PASSED"
  echo "NOTE: builds/tests require dependencies to be installed by the user first."
else
  echo "SOME CHECKS FAILED"
  exit 1
fi