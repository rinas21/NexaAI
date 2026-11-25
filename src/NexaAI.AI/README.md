# NexaAI Python AI Service

FastAPI service for the Enterprise AI Assistant. All model inference runs locally
through Ollama; no paid inference API is used. The ASP.NET API remains the owner
of pgvector, permissions and document storage.

## Features

- **RAG chat** with sanitization, PII masking, grounded prompt construction,
  citation extraction and output validation.
- **Retrieval** via the C# API (`POST /api/documents/search`, `X-API-Key`) with a
  local in-memory fallback for standalone use/tests. Local cosine re-ranking.
- **Agents** with Semantic Kernel integration when installed, a bounded
  tool-calling loop, an AST-safe calculator, datetime, document search and an
  approval-gated, parameterized read-only database tool.
- **Security**: prompt-injection guardrails plus Presidio REST PII detection
  with a regex fallback (email, phone, SSN, credit card).
- **MCP** tool server (FastMCP when available, JSON-RPC fallback).
- **Evaluation**: recall@k, MRR, nDCG, faithfulness, relevance, correctness and
  latency over a labelled sample set.

## Running

```bash
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Ollama must be running with the configured models:

```bash
ollama pull llama3.1:8b
ollama pull nomic-embed-text
```

## HTTP API

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/v1/embeddings/embed` | Generate embeddings |
| POST | `/api/v1/search/search` | Permission-filtered document search |
| POST | `/api/v1/rag/chat` | Grounded RAG answer with citations |
| POST | `/api/v1/agents/execute` | Run the tool-calling agent |
| GET | `/api/v1/agents/tools` | List agent tools |
| POST | `/api/v1/security/pii/detect` | Detect/mask PII |
| POST | `/api/v1/security/sanitize` | Sanitize untrusted text |
| POST | `/api/v1/evaluation/run` | Run an evaluation dataset |
| GET | `/mcp/tools` | List MCP tools |
| POST | `/mcp/tools/{name}/call` | Call an MCP tool |

Health checks: `/health` and `/health/detailed`; interactive docs: `/docs`.

## Configuration

Settings are read from environment variables or a `.env` file (see
`app/config.py`). Important ones:

- `OLLAMA_BASE_URL`, `OLLAMA_LLM_MODEL`, `OLLAMA_EMBEDDING_MODEL`
- `API_BASE_URL`, `API_KEY` (used for C# API calls, never hardcode real keys)
- `PRESIDIO_ENABLED`, `PRESIDIO_ANALYZER_URL`, `PRESIDIO_ANONYMIZER_URL`
- `RAG_TOP_K`, `RAG_SIMILARITY_THRESHOLD`, `MAX_INPUT_LENGTH`
- `FEATURE_ENABLE_MCP`, `FEATURE_ENABLE_EVALUATION`

Semantic Kernel, Presidio and the MCP SDK are optional: the service and its
unit tests work when they are not installed.

## Tests

```bash
python3 -m pytest tests -q
```

Unit tests mock Ollama and use the regex/local fallbacks, so no external
services are required. Sensitive tools (`database_query`, `delete_document`)
raise `ApprovalRequiredError` (HTTP 202 with an `approval_id`) unless explicitly
pre-approved through the request's `tools` list.
