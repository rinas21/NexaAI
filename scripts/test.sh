#!/usr/bin/env bash
# Run all automated tests.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> .NET tests"
dotnet test src/NexaAI.Tests --configuration Release

echo "==> Python tests"
(cd src/NexaAI.AI && python -m pytest tests -q)

echo "==> Frontend tests"
(cd src/NexaAI.Web && npm test -- --run)

echo "==> All tests complete"