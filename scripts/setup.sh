#!/usr/bin/env bash
# Initial setup: copy env template, generate secrets, pull models.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Enterprise AI Assistant setup"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from template."
fi

if command -v openssl >/dev/null 2>&1; then
  JWT_SECRET="$(openssl rand -base64 32 | tr -d '\n')"
  POSTGRES_PASSWORD="$(openssl rand -base64 24 | tr -d '\n')"
  AI_SERVICE_API_KEY="$(openssl rand -base64 24 | tr -d '\n')"
  NEO4J_PASSWORD="$(openssl rand -base64 24 | tr -d '\n')"

  sed -i "s|^JWT_SECRET=.*|JWT_SECRET=$JWT_SECRET|" .env
  sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$POSTGRES_PASSWORD|" .env
  sed -i "s|^AI_SERVICE_API_KEY=.*|AI_SERVICE_API_KEY=$AI_SERVICE_API_KEY|" .env
  sed -i "s|^NEO4J_PASSWORD=.*|NEO4J_PASSWORD=$NEO4J_PASSWORD|" .env
  echo "Generated secure random secrets in .env"
else
  echo "WARNING: openssl not found. Please edit .env manually."
fi

echo "==> Starting services"
docker compose -f docker/docker-compose.yml up -d --build

echo "==> Waiting for Ollama"
until docker exec nexai-ollama ollama list >/dev/null 2>&1; do sleep 3; done
docker exec nexai-ollama ollama pull nomic-embed-text
docker exec nexai-ollama ollama pull "${OLLAMA_LLM_MODEL:-llama3.1:8b}"

echo "==> Setup complete"
echo "Frontend: http://localhost:3000"
echo "API docs: http://localhost:5000/swagger"
echo "AI docs:  http://localhost:8000/docs"