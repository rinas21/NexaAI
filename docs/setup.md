# Detailed Setup Guide

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Docker + Compose | 24+ / v2 | Required |
| Git | any | |
| (Optional) NVIDIA GPU | any | With nvidia-container-toolkit for GPU inference |
| (Optional) .NET SDK | 8.0 | For local development without Docker |
| (Optional) Python | 3.12 | For local AI service development |
| (Optional) Node.js | 22 | For local frontend development |

CPU-only execution works with a smaller model (`ollama pull llama3.2:3b` and set `OLLAMA_LLM_MODEL=llama3.2:3b` in `.env`).

---

## 1. Configure Environment

```bash
cp .env.example .env
```

Generate secrets:

```bash
openssl rand -base64 32   # JWT_SECRET
openssl rand -base64 32   # POSTGRES_PASSWORD
openssl rand -base64 32   # AI_SERVICE_API_KEY
```

Edit `.env` and replace all `changeme_*` values.

---

## 2. Start Services

```bash
docker compose -f docker/docker-compose.yml up -d --build
```

This starts:
- PostgreSQL + pgvector (`:5432`)
- Ollama (`:11434`)
- ASP.NET Core API (`:5000`)
- Python AI service (`:8000`)
- React frontend (`:3000`)

First build takes several minutes (NuGet, pip, npm).

### Optional services

```bash
docker compose -f docker/docker-compose.yml --profile optional up -d
```

Adds Neo4j (`:7474`/`:7687`), Presidio Analyzer (`:3000` inside network), Presidio Anonymizer (`:3001`).

---

## 3. Pull Local Models

The `ollama` container has an init script that pulls `llama3.1:8b` and `nomic-embed-text` automatically. To do it manually:

```bash
docker exec nexai-ollama ollama pull llama3.1:8b
docker exec nexai-ollama ollama pull nomic-embed-text
```

For CPU-only machines:

```bash
docker exec nexai-ollama ollama pull llama3.2:3b
# then set OLLAMA_LLM_MODEL=llama3.2:3b in .env and restart
docker compose -f docker/docker-compose.yml restart api ai ollama
```

---

## 4. Verify

```bash
curl http://localhost:5000/health
curl http://localhost:8000/health
curl http://localhost:11434/api/tags
```

Open:
- Frontend → http://localhost:3000
- API docs → http://localhost:5000/swagger
- AI docs → http://localhost:8000/docs

Log in with the seeded admin account (see `docs/database-schema.md`).

---

## 5. Local Development (without Docker for one service)

### API
```bash
cd src/NexaAI.Api
dotnet restore && dotnet run
```

### AI service
```bash
cd src/NexaAI.AI
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

### Frontend
```bash
cd src/NexaAI.Web
npm install && npm run dev
```

Set `VITE_API_BASE_URL=http://localhost:5000` in `.env`.

---

## 6. Running Tests

```bash
# .NET
dotnet test src/NexaAI.Tests

# Python
cd src/NexaAI.AI && pytest tests -q

# Frontend
cd src/NexaAI.Web && npm test
```

---

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| Ollama not reachable | Check `docker logs nexai-ollama`; ensure GPU toolkit installed or switch to CPU model |
| Slow first answer | Model loading + cold start; wait ~30s |
| pgvector errors | Ensure `CREATE EXTENSION vector;` ran (`docker logs nexai-postgres`) |
| 401 on API calls | Token expired — use refresh endpoint or log in again |
| Presidio not detected | Start with `--profile optional`; check `ENABLE_PRESIDIO=true` |
| Frontend blank page | Check browser console; ensure API CORS allows `http://localhost:3000` |

---

## 8. Stopping / Resetting

```bash
docker compose -f docker/docker-compose.yml down
docker compose -f docker/docker-compose.yml down -v   # deletes all data
```
