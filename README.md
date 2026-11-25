# Enterprise AI Assistant

> **A local, zero-cost AI knowledge assistant built to demonstrate the technical skills required for a Senior Software Engineer – AI role.**

---

## 1. What Is This Project? (Read This First)

**Enterprise AI Assistant** is a **production-minded, fully local AI knowledge assistant**. It answers questions using **your own uploaded documents** (PDF/DOCX), cites its sources, runs **AI agents that call tools**, exposes those tools through **MCP**, protects data with **PII detection and authorization**, and **evaluates** its own answer quality.

### What kind of project is it?

It is an **end-to-end, multi-service but intentionally small AI application** — not a demo script and not a large microservice platform. It is designed to be run by **one developer on a laptop** with Docker, with **no paid APIs, no cloud AI services, and no API keys for AI inference**.

Every AI capability is **local**:

- LLM chat and reasoning → **Ollama**
- Embeddings → **Ollama**
- Vector search → **PostgreSQL + pgvector**
- PII detection → **Microsoft Presidio**
- Agent orchestration → **Microsoft Semantic Kernel**

### Why does it exist?

It is a **career-portfolio / interview-ready project** that proves hands-on ability in:

- Retrieval-Augmented Generation (RAG) with **grounded answers and citations**
- Agent design with **tool calling** and **human approval**
- **MCP** tool exposure and client integration
- **Security**: JWT, role-based access, document-level permissions, PII masking, prompt-injection defense
- **AI evaluation**: retrieval relevance, answer faithfulness, latency tracking
- **Clean architecture**, tests, migrations, Docker, and CI

### What it is NOT

- ❌ It does **not** call OpenAI, Claude, Gemini, or Azure OpenAI
- ❌ It does **not** claim commercial/cloud AI experience
- ❌ It is **not** a huge microservices system — it has exactly **three app services** (API, AI service, web) plus local infrastructure

---

## 2. Technology Stack — Each Component and What Kind It Is

| Component | Technology | Kind / Role in the Project | Why it was chosen |
|-----------|------------|---------------------------|-------------------|
| **Frontend** | **React 18 + TypeScript + Vite** | **Client UI** (browser dashboard) | Fast dev server, type-safe UI, modern component model |
| **Main Backend** | **ASP.NET Core Web API 8** | **Primary application API** — auth, users, documents, permissions, conversations, audit logs | Enterprise-grade RBAC, DI, middleware, EF Core, structured logging |
| **AI Service** | **Python 3.12 + FastAPI** | **AI microservice** — RAG pipeline, agent runtime, embeddings, MCP server, evaluation | Python has the best AI ecosystem (Semantic Kernel, Presidio, Ollama clients) |
| **AI Framework** | **Microsoft Semantic Kernel** | **Agent / orchestration framework** (Python) | Native function calling, planners, tool registry; matches enterprise .NET/AI stacks |
| **LLM** | **Ollama** (`llama3.1:8b`) | **Local language model runtime** — generates answers and reasons for agents | Zero cost, private, no API key |
| **Embeddings** | **Ollama** (`nomic-embed-text`, 768 dimensions) | **Local embedding model** — turns text into vectors | Zero cost, local, good retrieval quality |
| **Vector Database** | **PostgreSQL 16 + pgvector** | **Relational + vector storage** — chunks, embeddings, metadata, HNSW search | One DB for both relational data and vectors; ACID; simple ops |
| **Knowledge Graph** | **Neo4j 5** *(optional profile)* | **Graph database** — advanced entity/link module | Demonstrates optional graph-based retrieval |
| **Authentication** | **JWT + ASP.NET Core Identity-style roles** | **Security layer** — stateless access tokens + refresh tokens | Standard, stateless, RBAC-ready |
| **Authorization** | **Role-based + document-level access control** | **Security layer** — Admin/User roles + Private/Team/Public documents | Prevents unauthorized retrieval |
| **PII Protection** | **Microsoft Presidio** *(optional profile)* | **Security service** — detects and masks emails, phones, SSNs, credit cards | Enterprise-grade PII handling |
| **Vector Index** | **HNSW** (pgvector `vector_cosine_ops`) | **Index structure** — fast approximate nearest-neighbor search | Low latency semantic search |
| **MCP** | **Model Context Protocol (Python)** | **Tool protocol / integration layer** — exposes document search + approved tools | Emerging standard for AI tool interoperability |
| **Validation** | **FluentValidation + Zod** | **Input validation** (backend + frontend) | Clean, declarative validation |
| **Object Mapping** | **Mapster** | **DTO mapper** (backend) | Lightweight mapping |
| **Logging** | **Serilog (API) + structlog (AI)** | **Structured logging** | Searchable, context-rich logs |
| **Resilience** | **Polly** | **HTTP resilience** — retry + circuit breaker for AI/Ollama calls | Handles local service restarts gracefully |
| **Metrics** | **Prometheus client** | **Monitoring** — latency, token usage, tool results | Quantifies AI performance |
| **Database ORM** | **EF Core 8 + Npgsql** | **Data access** (backend) | Migrations, tracked entities, clean repository pattern |
| **Migrations** | **EF Core migrations + seed SQL** | **Schema management** | Reproducible database setup |
| **Testing** | **xUnit (C#) + pytest (Python) + Vitest (React)** | **Automated tests** | Unit + integration coverage |
| **Infrastructure** | **Docker Compose** | **Local orchestration** | One command to start everything |
| **CI/CD** | **GitHub Actions** | **Automation** — build, lint, test on every push | Quality gates |
| **Charts** | **Recharts** | **UI visualization** — evaluation dashboard | Shows metrics clearly |
| **API Docs** | **Swagger/OpenAPI** | **Documentation** — interactive API reference | Easy exploration |

### Architecture at a glance

```
Browser (React + TS)
        │  HTTPS / REST
        ▼
ASP.NET Core API  ──────► PostgreSQL + pgvector
   (auth, docs,          (users, docs, chunks, vectors, logs)
    permissions,
    conversations)
        │  HTTP + X-API-Key
        ▼
Python FastAPI AI Service ──► Ollama (LLM + embeddings)
   (RAG, agents,               (local inference, no paid API)
    MCP, Presidio,
    evaluation)
```

---

## 3. Quick Start (Local, Zero Cost)

### Prerequisites

- Docker 24+ with Docker Compose v2
- (Optional) NVIDIA GPU for faster inference — CPU works with smaller models
- 16 GB RAM recommended

### Steps

```bash
# 1. Clone
git clone <repo-url> NexaAI && cd NexaAI

# 2. Configure environment
cp .env.example .env
# Edit .env: set POSTGRES_PASSWORD, JWT_SECRET, AI_SERVICE_API_KEY

# 3. Start all services
docker compose -f docker/docker-compose.yml up -d --build

# 4. Pull local models (first run only)
docker exec nexai-ollama ollama pull llama3.1:8b
docker exec nexai-ollama ollama pull nomic-embed-text

# 5. Open the app
# Frontend:  http://localhost:3000
# API docs:  http://localhost:5000/swagger
# AI docs:   http://localhost:8000/docs
```

Optional services (Neo4j, Presidio):

```bash
docker compose -f docker/docker-compose.yml --profile optional up -d
```

---

## 4. Core Features

1. **Authentication & User Management** — registration, login, JWT + refresh tokens, Admin/User roles
2. **Document Knowledge Base** — PDF/DOCX upload, text extraction, chunking, local embeddings, pgvector storage, semantic search with metadata filters
3. **RAG Chat Assistant** — grounded answers with citations, conversation history, refuses unsupported answers, enforces document permissions
4. **AI Agents & Tool Calling** — Semantic Kernel agent with document search, database query, calculator, datetime tools; human approval for sensitive tools; execution logs
5. **MCP Integration** — Python MCP server exposing document search + business tools; client integration demo
6. **AI Security & Guardrails** — Presidio PII masking, prompt-injection defense, output validation, authorization before tool use
7. **AI Evaluation & Monitoring** — small eval dataset, retrieval + generation metrics, latency/token tracking, failure logs
8. **Frontend Dashboard** — chat, documents, agent tools, evaluation charts

---

## 5. Repository Structure

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full structure, database schema, API endpoint list, milestones, and ADRs.

```
docker/          → Docker Compose + Dockerfiles
src/NexaAI.Api/  → ASP.NET Core Web API (main backend)
src/NexaAI.Core/ → Shared domain/application/infrastructure
src/NexaAI.AI/   → Python FastAPI AI service
src/NexaAI.Web/  → React + TypeScript frontend
database/        → migrations + seed data
docs/            → API, schema, setup, ADRs
scripts/         → setup/start/stop helpers
```

---

## 6. Documentation Index

- [ARCHITECTURE.md](ARCHITECTURE.md) — structure, milestones, schema, endpoints, ADRs
- [docs/architecture-diagram.md](docs/architecture-diagram.md) — Mermaid system/RAG/agent diagrams
- [docs/api-spec.md](docs/api-spec.md) — API reference
- [docs/database-schema.md](docs/database-schema.md) — database schema
- [docs/setup.md](docs/setup.md) — detailed setup
- [docs/adr/](docs/adr/) — architecture decision records

---

## 7. Milestones (Incremental Build)

Each milestone is built and verified before the next:

1. Foundation & Infrastructure ✅ (in progress)
2. Authentication & User Management
3. Document Knowledge Base
4. RAG Chat Assistant
5. AI Agents & Tool Calling
6. MCP Integration
7. AI Security & Guardrails
8. AI Evaluation & Monitoring
9. Frontend Polish & Integration
10. Documentation & Deployment

---

## 8. Demonstration Scenarios

Sample documents are provided in [`docs/demo-documents/`](docs/demo-documents/) (generate with `python3 scripts/generate-demo-docs.py`). A 20-question evaluation dataset is in [`database/seed/evaluation-dataset.jsonl`](database/seed/evaluation-dataset.jsonl).

| Scenario | What it shows |
|----------|---------------|
| Upload `hr-handbook.docx`, ask "How many vacation days?" | RAG + citations + local embeddings |
| Ask for someone's email in a prompt | PII masking via Presidio |
| Try to inject "ignore previous instructions" | Injection defense |
| Agent asks to delete a document | Human approval workflow |
| Call `document_search` via MCP client | MCP tool exposure |
| Admin views evaluation dashboard | Retrieval/quality metrics |

### Screenshots

Screenshots will be placed in [`docs/screenshots/`](docs/screenshots/) after the UI is running:

| View | File |
|------|------|
| Chat with citations | `docs/screenshots/chat.png` |
| Document management | `docs/screenshots/documents.png` |
| Agent tools & approvals | `docs/screenshots/agents.png` |
| Evaluation dashboard | `docs/screenshots/evaluation.png` |

> Generate them locally with your browser after starting the app (`http://localhost:3000`).

---

## 9. License & Notes

- All AI inference runs locally through Ollama. No paid AI APIs are used.
- This project demonstrates **local implementation skills**; it does not claim commercial Azure/AI cloud experience.
- Credentials are environment variables only — never hardcoded.
