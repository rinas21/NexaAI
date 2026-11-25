# Enterprise AI Assistant - Architecture & Implementation Plan

## Repository Structure

```
NexaAI/
├── .github/
│   └── workflows/
│       ├── ci.yml              # CI pipeline
│       └── cd.yml              # CD pipeline (optional)
├── docs/
│   ├── architecture.md         # This file
│   ├── api-spec.md             # OpenAPI/Swagger documentation
│   ├── database-schema.md      # Database schema documentation
│   ├── setup.md                # Setup instructions
│   └── adr/                    # Architecture Decision Records
├── docker/
│   ├── docker-compose.yml      # Main docker compose
│   ├── docker-compose.override.yml.example
│   ├── Dockerfile.api          # ASP.NET Core API
│   ├── Dockerfile.ai           # Python AI Service
│   ├── Dockerfile.web          # React Frontend
│   └── .dockerignore
├── src/
│   ├── NexaAI.Api/             # ASP.NET Core Web API (Main Backend)
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Middleware/
│   │   ├── Extensions/
│   │   ├── Program.cs
│   │   └── NexaAI.Api.csproj
│   ├── NexaAI.Core/            # Shared Core Library
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── NexaAI.Core.csproj
│   ├── NexaAI.AI/              # Python FastAPI AI Service
│   │   ├── app/
│   │   │   ├── api/
│   │   │   ├── core/
│   │   │   ├── services/
│   │   │   ├── agents/
│   │   │   ├── mcp/
│   │   │   ├── security/
│   │   │   ├── evaluation/
│   │   │   └── main.py
│   │   ├── requirements.txt
│   │   ├── pyproject.toml
│   │   └── Dockerfile
│   ├── NexaAI.Web/             # React + TypeScript Frontend
│   │   ├── src/
│   │   │   ├── components/
│   │   │   ├── pages/
│   │   │   ├── hooks/
│   │   │   ├── services/
│   │   │   ├── store/
│   │   │   ├── types/
│   │   │   ├── utils/
│   │   │   ├── App.tsx
│   │   │   └── main.tsx
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── vite.config.ts
│   │   └── Dockerfile
│   └── NexaAI.Tests/           # Test Projects
│       ├── NexaAI.Api.Tests/
│       ├── NexaAI.Core.Tests/
│       └── NexaAI.AI.Tests/
├── database/
│   ├── migrations/             # EF Core migrations
│   └── seed/                   # Seed data scripts
├── scripts/
│   ├── setup.sh                # Initial setup script
│   ├── start.sh                # Start all services
│   ├── stop.sh                 # Stop all services
│   └── test.sh                 # Run tests
├── .env.example                # Environment variables template
├── .gitignore
├── README.md
└── LICENSE
```

## Technology Stack Summary

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React + TypeScript + Vite | 18.x / 5.x |
| Main Backend | ASP.NET Core Web API | 8.0 |
| AI Service | Python + FastAPI | 3.12 / 0.109+ |
| AI Framework | Microsoft Semantic Kernel | 1.0+ |
| LLM | Ollama (local) | Latest |
| Embeddings | Ollama (nomic-embed-text) | Latest |
| Vector DB | PostgreSQL + pgvector | 16 / 0.7+ |
| Graph DB | Neo4j | 5.x (optional) |
| Auth | JWT + ASP.NET Core Identity | 8.0 |
| PII Protection | Microsoft Presidio | 2.2+ |
| Containerization | Docker Compose | v2 |
| CI/CD | GitHub Actions | Latest |

## Implementation Milestones

### Milestone 1: Foundation & Infrastructure (Week 1)
**Goal**: Core infrastructure running with basic API

- [ ] Repository structure setup
- [ ] Docker Compose with PostgreSQL, pgvector, Ollama, Neo4j
- [ ] ASP.NET Core Web API project with clean architecture
- [ ] Python FastAPI AI Service project structure
- [ ] React + TypeScript + Vite frontend project
- [ ] Environment configuration (.env.example)
- [ ] Database schema design and EF Core migrations
- [ ] Basic health check endpoints
- [ ] CI pipeline (build, lint, test)

**Verification**: `docker-compose up -d` starts all services; API returns 200 on `/health`

---

### Milestone 2: Authentication & User Management (Week 1-2)
**Goal**: Secure authentication with RBAC

- [ ] User registration endpoint
- [ ] User login with JWT token generation
- [ ] JWT middleware and validation
- [ ] Role-based authorization (Admin, User)
- [ ] Password hashing (BCrypt)
- [ ] Refresh token mechanism
- [ ] User management endpoints (Admin only)
- [ ] Frontend: Login/Register pages, protected routes
- [ ] Unit tests for auth logic

**Verification**: Can register, login, access protected endpoints with valid JWT

---

### Milestone 3: Document Knowledge Base (Week 2-3)
**Goal**: Document upload, processing, and vector storage

- [ ] Document upload endpoint (PDF, DOCX)
- [ ] Document metadata storage (PostgreSQL)
- [ ] Text extraction (PDF: PdfPig, DOCX: OpenXml)
- [ ] Text chunking strategy (semantic/recursive)
- [ ] Embedding generation via Ollama (nomic-embed-text)
- [ ] pgvector storage with document/chunk metadata
- [ ] Semantic search endpoint with metadata filtering
- [ ] Document-level permissions (RBAC)
- [ ] Frontend: Document upload, list, delete, view
- [ ] Integration tests for document pipeline

**Verification**: Upload PDF → chunks stored in pgvector → semantic search returns relevant chunks

---

### Milestone 4: RAG Chat Assistant (Week 3)
**Goal**: Conversational AI with document grounding

- [ ] Chat endpoint with conversation history
- [ ] RAG pipeline: query → embed → retrieve → generate
- [ ] Source citation in responses
- [ ] Conversation persistence
- [ ] Streaming responses (Server-Sent Events)
- [ ] Guardrails: refuse unsupported answers
- [ ] Document permission enforcement in retrieval
- [ ] Frontend: Chat interface with citations, history sidebar
- [ ] Evaluation dataset creation (10-20 Q&A pairs)

**Verification**: Ask question → get answer with citations from uploaded docs

---

### Milestone 5: AI Agents & Tool Calling (Week 3-4)
**Goal**: Semantic Kernel agent with tools

- [ ] Semantic Kernel setup in Python service
- [ ] Tool: Document Search (semantic + keyword)
- [ ] Tool: Database Query (parameterized, read-only)
- [ ] Tool: Calculator
- [ ] Tool: Current DateTime
- [ ] Human approval workflow for sensitive tools
- [ ] Execution logging (audit trail)
- [ ] Agent planning and execution loop
- [ ] Frontend: Agent tools panel, execution logs

**Verification**: Agent answers "What's in doc X?" by calling search tool

---

### Milestone 6: MCP Integration (Week 4)
**Goal**: Model Context Protocol server and client

- [ ] MCP server in Python (FastMCP or raw)
- [ ] Expose: document_search, get_document, list_documents
- [ ] Expose: approved business tools (calculator, datetime)
- [ ] MCP client integration in AI service
- [ ] Demonstrate external MCP client connection
- [ ] Frontend: MCP connection status, tool discovery

**Verification**: MCP client can call document_search tool

---

### Milestone 7: AI Security & Guardrails (Week 4-5)
**Goal**: Production-grade security

- [ ] PII detection and masking (Presidio)
- [ ] Input sanitization (prompt injection protection)
- [ ] Output validation (refusal detection, hallucination check)
- [ ] Authorization checks before tool execution
- [ ] Document access control in RAG retrieval
- [ ] Rate limiting on AI endpoints
- [ ] Audit logging for all AI interactions
- [ ] Frontend: Security dashboard (PII masked, blocked requests)

**Verification**: PII in prompt → masked; injection attempt → blocked; unauthorized doc access → denied

---

### Milestone 8: AI Evaluation & Monitoring (Week 5)
**Goal**: Measurable AI quality

- [ ] Evaluation dataset (JSONL: question, expected_answer, context_docs)
- [ ] Retrieval metrics: Recall@k, MRR, nDCG
- [ ] Generation metrics: Faithfulness, Answer Relevance, Correctness
- [ ] Latency tracking (p50, p95, p99)
- [ ] Token usage tracking
- [ ] Failure logging and alerting
- [ ] Frontend: Evaluation dashboard with charts
- [ ] Automated evaluation in CI (optional)

**Verification**: Run evaluation → get metrics report

---

### Milestone 9: Frontend Polish & Integration (Week 5-6)
**Goal**: Production-ready UI

- [ ] Responsive layout with navigation
- [ ] Dark/light theme
- [ ] Real-time streaming chat
- [ ] Document management (drag-drop, preview)
- [ ] Agent tool visualization
- [ ] Evaluation dashboard (charts with Recharts)
- [ ] Settings page (model config, API keys)
- [ ] Error boundaries and loading states
- [ ] Accessibility (WCAG AA)

**Verification**: Full end-to-end workflows work in browser

---

### Milestone 10: Documentation & Deployment (Week 6)
**Goal**: Complete deliverables

- [ ] Architecture diagram (Mermaid)
- [ ] Database schema diagram
- [ ] API documentation (Swagger/OpenAPI)
- [ ] README with screenshots
- [ ] Setup instructions
- [ ] ADR documents
- [ ] Docker production compose
- [ ] GitHub Actions CD (optional)
- [ ] Final integration testing

---

## Database Schema (PostgreSQL)

```sql
-- Users & Auth
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255),
    role VARCHAR(50) NOT NULL DEFAULT 'User', -- 'Admin', 'User'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    revoked_at TIMESTAMPTZ
);

-- Documents
CREATE TABLE documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(500) NOT NULL,
    original_filename VARCHAR(500) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    file_size BIGINT NOT NULL,
    storage_path VARCHAR(1000) NOT NULL,
    owner_id UUID REFERENCES users(id) ON DELETE SET NULL,
    access_level VARCHAR(50) NOT NULL DEFAULT 'Private', -- 'Private', 'Team', 'Public'
    status VARCHAR(50) NOT NULL DEFAULT 'Processing', -- 'Processing', 'Ready', 'Failed'
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_documents_owner ON documents(owner_id);
CREATE INDEX idx_documents_access ON documents(access_level);

-- Document Chunks (with pgvector)
CREATE TABLE document_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,
    content TEXT NOT NULL,
    token_count INT NOT NULL,
    embedding vector(768), -- nomic-embed-text dimension
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_chunks_document ON document_chunks(document_id);
CREATE INDEX idx_chunks_embedding ON document_chunks USING hnsw (embedding vector_cosine_ops);

-- Conversations
CREATE TABLE conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(500),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID REFERENCES conversations(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL, -- 'user', 'assistant', 'system', 'tool'
    content TEXT NOT NULL,
    citations JSONB DEFAULT '[]', -- [{doc_id, chunk_id, snippet}]
    tool_calls JSONB DEFAULT '[]',
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_messages_conversation ON messages(conversation_id);

-- Agent Execution Logs
CREATE TABLE agent_executions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    agent_name VARCHAR(100) NOT NULL,
    input JSONB NOT NULL,
    output JSONB,
    status VARCHAR(50) NOT NULL, -- 'Running', 'Completed', 'Failed', 'PendingApproval'
    error_message TEXT,
    execution_time_ms INT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE INDEX idx_agent_exec_user ON agent_executions(user_id);
CREATE INDEX idx_agent_exec_status ON agent_executions(status);

-- Tool Approvals
CREATE TABLE tool_approvals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    execution_id UUID REFERENCES agent_executions(id) ON DELETE CASCADE,
    tool_name VARCHAR(100) NOT NULL,
    parameters JSONB NOT NULL,
    requested_by UUID REFERENCES users(id) ON DELETE SET NULL,
    approved_by UUID REFERENCES users(id) ON DELETE SET NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected'
    reason TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    decided_at TIMESTAMPTZ
);

-- Evaluation Runs
CREATE TABLE evaluation_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    dataset_version VARCHAR(100),
    config JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Running',
    started_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE TABLE evaluation_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id UUID REFERENCES evaluation_runs(id) ON DELETE CASCADE,
    question_id VARCHAR(100) NOT NULL,
    question TEXT NOT NULL,
    expected_answer TEXT,
    actual_answer TEXT,
    retrieved_chunks JSONB,
    metrics JSONB NOT NULL, -- {recall, mrr, faithfulness, relevance, latency_ms}
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

## API Endpoints Overview

### Authentication
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/auth/register` | Register new user | None |
| POST | `/api/auth/login` | Login, return JWT + refresh | None |
| POST | `/api/auth/refresh` | Refresh access token | Refresh Token |
| POST | `/api/auth/logout` | Revoke refresh token | JWT |
| GET | `/api/auth/me` | Current user profile | JWT |
| PUT | `/api/auth/me` | Update profile | JWT |
| GET | `/api/auth/users` | List users (Admin) | JWT + Admin |
| PUT | `/api/auth/users/{id}/role` | Update user role (Admin) | JWT + Admin |

### Documents
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/documents` | Upload document | JWT |
| GET | `/api/documents` | List documents (with filters) | JWT |
| GET | `/api/documents/{id}` | Get document details | JWT + Perm |
| DELETE | `/api/documents/{id}` | Delete document | JWT + Perm |
| GET | `/api/documents/{id}/chunks` | List document chunks | JWT + Perm |
| POST | `/api/documents/{id}/reprocess` | Reprocess document | JWT + Admin |

### Chat (RAG)
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/chat` | Send message, get RAG response | JWT |
| GET | `/api/chat/conversations` | List conversations | JWT |
| POST | `/api/chat/conversations` | Create conversation | JWT |
| GET | `/api/chat/conversations/{id}` | Get conversation with messages | JWT |
| DELETE | `/api/chat/conversations/{id}` | Delete conversation | JWT |
| POST | `/api/chat/conversations/{id}/messages` | Add message (streaming) | JWT |

### Agents
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/agents/execute` | Execute agent with tools | JWT |
| GET | `/api/agents/executions` | List execution history | JWT |
| GET | `/api/agents/executions/{id}` | Get execution details | JWT |
| POST | `/api/agents/approvals/{id}/approve` | Approve tool execution | JWT + Admin |
| POST | `/api/agents/approvals/{id}/reject` | Reject tool execution | JWT + Admin |

### MCP
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/mcp/tools` | List available MCP tools | JWT |
| POST | `/mcp/tools/{name}/call` | Call MCP tool | JWT |

### Evaluation
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/evaluation/run` | Start evaluation run | JWT + Admin |
| GET | `/api/evaluation/runs` | List evaluation runs | JWT + Admin |
| GET | `/api/evaluation/runs/{id}` | Get run results | JWT + Admin |
| GET | `/api/evaluation/metrics/summary` | Get aggregated metrics | JWT + Admin |

### Health & Monitoring
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/health` | Basic health check | None |
| GET | `/health/detailed` | Detailed health (DB, Ollama, etc.) | JWT + Admin |
| GET | `/metrics` | Prometheus metrics | None |

## Docker Compose Services

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: nexai
      POSTGRES_USER: nexai
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./database/init:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U nexai"]
      interval: 10s
      timeout: 5s
      retries: 5

  neo4j:
    image: neo4j:5
    environment:
      NEO4J_AUTH: neo4j/${NEO4J_PASSWORD}
      NEO4J_PLUGINS: '["apoc"]'
    volumes:
      - neo4j_data:/data
    profiles: ["optional"]

  ollama:
    image: ollama/ollama:latest
    volumes:
      - ollama_data:/root/.ollama
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    healthcheck:
      test: ["CMD", "ollama", "list"]
      interval: 30s
      timeout: 10s
      retries: 3

  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile.api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Host=postgres;Database=nexai;Username=nexai;Password=${POSTGRES_PASSWORD}
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=NexaAI
      - Jwt__Audience=NexaAI
      - Ollama__BaseUrl=http://ollama:11434
      - AIService__BaseUrl=http://ai:8000
    ports:
      - "5000:8080"
    depends_on:
      postgres:
        condition: service_healthy
      ollama:
        condition: service_healthy

  ai:
    build:
      context: ..
      dockerfile: docker/Dockerfile.ai
    environment:
      - OLLAMA_BASE_URL=http://ollama:11434
      - EMBEDDING_MODEL=nomic-embed-text
      - LLM_MODEL=llama3.1:8b
      - PRESEDIO_LANGUAGE=en
    ports:
      - "8000:8000"
    depends_on:
      ollama:
        condition: service_healthy

  web:
    build:
      context: ..
      dockerfile: docker/Dockerfile.web
    ports:
      - "3000:80"
    depends_on:
      - api

volumes:
  postgres_data:
  neo4j_data:
  ollama_data:
```

## Environment Variables (.env.example)

```bash
# Database
POSTGRES_PASSWORD=changeme_secure_password

# Neo4j (optional)
NEO4J_PASSWORD=changeme_neo4j_password

# JWT
JWT_SECRET=your-super-secret-jwt-key-min-32-chars
JWT_ISSUER=NexaAI
JWT_AUDIENCE=NexaAI

# Ollama
OLLAMA_BASE_URL=http://localhost:11434
EMBEDDING_MODEL=nomic-embed-text
LLM_MODEL=llama3.1:8b

# AI Service
AI_SERVICE_BASE_URL=http://localhost:8000

# Presidio
PRESIDIO_LANGUAGE=en

# Frontend
VITE_API_BASE_URL=http://localhost:5000
```

## Key Design Decisions (ADRs)

### ADR-001: Dual Backend Architecture (ASP.NET Core + Python FastAPI)
**Decision**: Separate main API (C#) from AI service (Python)
**Rationale**: 
- C# excels at enterprise web APIs, auth, RBAC, database operations
- Python excels at AI/ML ecosystem (Semantic Kernel, Presidio, LangChain)
- Clear separation of concerns, independent scaling
- Team can work in parallel on both services

### ADR-002: Local-First with Ollama
**Decision**: All LLM inference and embeddings via local Ollama
**Rationale**:
- Zero cost, no API keys, data privacy
- Meets "no paid APIs" constraint
- Models: llama3.1:8b (chat), nomic-embed-text (embeddings)
- Trade-off: Requires GPU for acceptable performance

### ADR-003: pgvector for Vector Storage
**Decision**: PostgreSQL + pgvector instead of dedicated vector DB
**Rationale**:
- Single database for relational + vector data
- ACID transactions across document metadata and vectors
- Mature ecosystem, EF Core support
- Sufficient for <1M vectors (project scope)

### ADR-004: Semantic Kernel for Agents
**Decision**: Microsoft Semantic Kernel as agent framework
**Rationale**:
- Native C# and Python support
- Built-in planner, function calling, memory
- Enterprise-grade, actively maintained
- Aligns with "Senior SWE - AI" skill demonstration

### ADR-005: MCP for Tool Exposure
**Decision**: Implement MCP server for tool exposure
**Rationale**:
- Demonstrates emerging standard (MCP)
- Enables external client integration
- Decouples tool definitions from AI service

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Ollama performance on CPU | High | Document GPU requirement; use smaller models (llama3.2:3b) for CPU |
| pgvector HNSW index memory | Medium | Monitor memory; use IVFFlat for larger datasets |
| Semantic Kernel Python maturity | Medium | Pin versions; have fallback to raw function calling |
| Presidio false positives | Low | Tunable confidence thresholds; allowlist for domain terms |
| Frontend complexity creep | Medium | Strict component library; defer nice-to-haves |

## Next Steps

1. **Initialize repository** with folder structure and root files
2. **Create Docker Compose** and verify all services start
3. **Build ASP.NET Core API** with clean architecture
4. **Build Python AI Service** with FastAPI structure
5. **Build React Frontend** with Vite + TypeScript
6. **Implement Milestone 1** completely before proceeding

---

*This document serves as the single source of truth for architecture. Update as decisions evolve.*