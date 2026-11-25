# API Documentation

## Base URLs

| Service | Base URL | Docs |
|---------|----------|------|
| ASP.NET Core API | `http://localhost:5000` | [Swagger UI](http://localhost:5000/swagger) |
| Python AI Service | `http://localhost:8000` | [OpenAPI UI](http://localhost:8000/docs) |

All protected endpoints require `Authorization: Bearer <JWT>` unless noted.

Internal calls from the API to the AI service use the `X-API-Key` header with the value of `AI_SERVICE_API_KEY`.

---

## Authentication (`/api/auth`)

### `POST /api/auth/register`
Register a new standard user. Returns access + refresh tokens.

```json
{ "email": "user@example.com", "password": "Password123!", "fullName": "Jane Doe" }
```

### `POST /api/auth/login`
```json
{ "email": "user@example.com", "password": "Password123!" }
```
Response:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "expiresAt": "2026-11-25T12:00:00Z",
  "user": { "id": "...", "email": "...", "role": "User" }
}
```

### `POST /api/auth/refresh`
```json
{ "refreshToken": "..." }
```

### `POST /api/auth/logout`
Revokes the refresh token. Requires JWT.

### `GET /api/auth/me`
Returns current user profile. Requires JWT.

### `GET /api/auth/users`
List all users. **Admin only**.

### `PUT /api/auth/users/{id}/role`
```json
{ "role": "Admin" }
```
**Admin only**.

---

## Documents (`/api/documents`)

### `POST /api/documents`
`multipart/form-data`: `file` (PDF/DOCX, max 50 MB), `title`, `accessLevel` (`Private` | `Team` | `Public`).

Returns document metadata with status `Processing`. The AI service extracts text, chunks it, generates embeddings and updates status to `Ready`.

### `GET /api/documents`
Query params: `status`, `accessLevel`, `page`, `pageSize`. Returns only documents the caller can access.

### `GET /api/documents/{id}`
Document details. Permission enforced.

### `DELETE /api/documents/{id}`
Owner or Admin only.

### `GET /api/documents/{id}/chunks`
List stored chunks (metadata + content, no embedding payload).

### `POST /api/documents/{id}/reprocess`
**Admin only**. Re-runs extraction, chunking and embedding.

### `POST /api/documents/search`
```json
{
  "query": "What is the refund policy?",
  "topK": 10,
  "similarityThreshold": 0.7,
  "metadataFilter": { "department": "HR" }
}
```
Returns semantically similar chunks with scores. Retrieval is restricted to documents the caller may read.

---

## Chat / RAG (`/api/chat`)

### `POST /api/chat`
```json
{
  "message": "Summarize the refund policy.",
  "conversationId": null,
  "useAgent": false
}
```
Response:
```json
{
  "conversationId": "...",
  "messageId": "...",
  "content": "Refunds are available within 30 days...",
  "citations": [
    { "documentId": "...", "chunkId": "...", "documentTitle": "policy.pdf", "snippet": "...", "similarityScore": 0.86 }
  ],
  "isGrounded": true,
  "refusalReason": null,
  "tokenUsage": { "promptTokens": 512, "completionTokens": 88, "totalTokens": 600 }
}
```

If no relevant chunks are found above threshold, `isGrounded` is `false`, `refusalReason` is set, and the answer is a refusal (no unsupported generation).

### `GET /api/chat/conversations`
### `POST /api/chat/conversations` — `{ "title": "..." }`
### `GET /api/chat/conversations/{id}` — includes messages with citations and tool calls
### `DELETE /api/chat/conversations/{id}`

---

## Agents (`/api/agents`)

### `POST /api/agents/execute`
```json
{
  "agentName": "knowledge-agent",
  "input": "Find the refund policy and calculate 15% of 200",
  "requireApproval": false
}
```

Available tools (discoverable via `GET /api/agents/tools`):

| Tool | Description | Requires approval |
|------|-------------|-------------------|
| `document_search` | Semantic search over accessible documents | No |
| `calculator` | Safe arithmetic expression evaluation | No |
| `datetime` | Current date/time in ISO format | No |
| `database_query` | Parameterized read-only SQL (allowlisted tables) | **Yes** |
| `delete_document` | Delete a document | **Yes** |

When a sensitive tool is requested and `requireApproval` is true, the execution status becomes `PendingApproval`, a `tool_approvals` row is created, and the response includes the `approvalId`.

### `GET /api/agents/executions`
### `GET /api/agents/executions/{id}`
### `GET /api/agents/approvals`
**Admin only**. Pending approvals.

### `POST /api/agents/approvals/{id}/approve`
### `POST /api/agents/approvals/{id}/reject`
**Admin only**. Body: `{ "reason": "..." }`.

---

## MCP (`/mcp`)

### `GET /mcp/tools`
List tools exposed by the Python MCP server.

### `POST /mcp/tools/{name}/call`
```json
{ "arguments": { "query": "refund policy", "topK": 5 } }
```
All calls are authorized: the caller's JWT identity and document permissions are forwarded to the AI service.

---

## Evaluation (`/api/evaluation`)

All endpoints **Admin only**.

### `POST /api/evaluation/run`
```json
{ "name": "v1-baseline", "datasetVersion": "1.0", "config": { "topK": 5 } }
```

### `GET /api/evaluation/runs`
### `GET /api/evaluation/runs/{id}`
### `GET /api/evaluation/metrics/summary`
Returns:
```json
{
  "totalRuns": 3,
  "averageRecall": 0.82,
  "averageMrr": 0.71,
  "averageFaithfulness": 0.88,
  "averageAnswerRelevance": 0.9,
  "averageLatencyMs": 1240,
  "p95LatencyMs": 2800,
  "totalTokensUsed": 45000,
  "totalFailures": 2
}
```

---

## Health (`/health`)

### `GET /health` — basic liveness
### `GET /health/detailed` — DB, Ollama, AI service status (Admin)

---

## AI Service Endpoints (internal)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/v1/embeddings` | Generate embeddings via Ollama |
| POST | `/api/v1/search` | Semantic retrieval (permission-filtered via API) |
| POST | `/api/v1/rag` | Full grounded RAG generation |
| POST | `/api/v1/agents/execute` | Semantic Kernel agent execution |
| GET | `/api/v1/agents/tools` | List agent tools |
| POST | `/api/v1/security/pii/detect` | Presidio/regex PII detection + masking |
| POST | `/api/v1/security/sanitize` | Prompt injection detection + sanitization |
| POST | `/api/v1/evaluation/run` | Evaluate dataset |
| GET | `/mcp/tools` | MCP tool discovery |
| POST | `/mcp/tools/{name}/call` | MCP tool invocation |
| GET | `/health` | AI service health |

---

## Error Format

```json
{
  "status": 400,
  "title": "Validation failed",
  "detail": "...",
  "instance": "/api/documents",
  "traceId": "...",
  "errors": [{ "field": "Email", "errors": ["Invalid email"] }]
}
```

AI service errors:
```json
{
  "error": "PROMPT_INJECTION",
  "error_code": "PROMPT_INJECTION",
  "message": "Prompt injection detected",
  "details": {},
  "timestamp": "2026-11-25T12:00:00Z"
}
```
