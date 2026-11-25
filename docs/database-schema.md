# Database Schema

PostgreSQL 16 + pgvector 0.7+. Schema is created by `database/init/001_init.sql` on first container start and by EF Core migrations thereafter.

## Entity Relationship Diagram

```mermaid
erDiagram
    USERS ||--o{ REFRESH_TOKENS : has
    USERS ||--o{ DOCUMENTS : owns
    USERS ||--o{ CONVERSATIONS : has
    USERS ||--o{ AGENT_EXECUTIONS : triggers
    DOCUMENTS ||--o{ DOCUMENT_CHUNKS : contains
    CONVERSATIONS ||--o{ MESSAGES : contains
    AGENT_EXECUTIONS ||--o{ TOOL_APPROVALS : requires
    EVALUATION_RUNS ||--o{ EVALUATION_RESULTS : produces

    USERS {
        uuid id PK
        varchar email UK
        varchar password_hash
        varchar full_name
        varchar role
        boolean is_active
        timestamptz created_at
    }
    DOCUMENTS {
        uuid id PK
        varchar title
        varchar original_filename
        varchar content_type
        bigint file_size
        varchar storage_path
        uuid owner_id FK
        varchar access_level
        varchar status
        jsonb metadata
    }
    DOCUMENT_CHUNKS {
        uuid id PK
        uuid document_id FK
        int chunk_index
        text content
        int token_count
        vector_768 embedding
        jsonb metadata
    }
    CONVERSATIONS {
        uuid id PK
        uuid user_id FK
        varchar title
    }
    MESSAGES {
        uuid id PK
        uuid conversation_id FK
        varchar role
        text content
        jsonb citations
        jsonb tool_calls
    }
    AGENT_EXECUTIONS {
        uuid id PK
        uuid user_id FK
        varchar agent_name
        jsonb input
        jsonb output
        varchar status
        int execution_time_ms
    }
    TOOL_APPROVALS {
        uuid id PK
        uuid execution_id FK
        varchar tool_name
        jsonb parameters
        varchar status
    }
    EVALUATION_RUNS {
        uuid id PK
        varchar name
        jsonb config
        varchar status
    }
    EVALUATION_RESULTS {
        uuid id PK
        uuid run_id FK
        varchar question_id
        text question
        jsonb metrics
    }
```

## Tables

### `users`
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | `gen_random_uuid()` |
| email | varchar(255) UK | Lowercase, unique |
| password_hash | varchar(255) | BCrypt |
| full_name | varchar(255) | Optional |
| role | varchar(50) | `Admin` / `User`, default `User` |
| is_active | boolean | Soft delete, default true |
| created_at / updated_at | timestamptz | UTC |

### `documents`
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| title / original_filename | varchar(500) | |
| content_type | varchar(100) | `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| file_size | bigint | Bytes |
| storage_path | varchar(1000) | Local uploads path |
| owner_id | uuid FK → users | SET NULL |
| access_level | varchar(50) | `Private` / `Team` / `Public` |
| status | varchar(50) | `Processing` / `Ready` / `Failed` |
| metadata | jsonb | Page count, chunking config, etc. |

### `document_chunks`
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| document_id | uuid FK → documents | CASCADE delete |
| chunk_index | int | Unique per document |
| content | text | Extracted text chunk |
| token_count | int | Approximate token count |
| embedding | vector(768) | nomic-embed-text output |
| metadata | jsonb | Page number, heading, overlap info |

**Indexes**
- `ix_document_chunks_document_id` (btree)
- `ix_document_chunks_document_chunk_index` (unique composite)
- `ix_document_chunks_embedding` — **HNSW**, `vector_cosine_ops`, `m=16`, `ef_construction=64`

### `conversations` / `messages`
Conversation belongs to a user; messages belong to a conversation. `role` ∈ `user`, `assistant`, `system`, `tool`. `citations` and `tool_calls` are `jsonb`.

### `agent_executions` / `tool_approvals`
Execution status: `Running`, `Completed`, `Failed`, `PendingApproval`. Tool approvals record requested parameters, decided-by admin, and reason.

### `evaluation_runs` / `evaluation_results`
Run config and per-question metrics stored as `jsonb` (recall@k, MRR, nDCG, faithfulness, answer relevance, correctness, latency).

## Vector Search Query Pattern

```sql
SELECT
    dc.id, dc.document_id, dc.content,
    1 - (dc.embedding <=> $1::vector) AS similarity
FROM document_chunks dc
JOIN documents d ON d.id = dc.document_id
WHERE dc.embedding IS NOT NULL
  AND d.status = 'Ready'
  AND (d.access_level = 'Public' OR d.owner_id = $2 OR $3 = true)
ORDER BY dc.embedding <=> $1::vector
LIMIT $4;
```

`$1` = query embedding, `$2` = current user id, `$3` = is admin, `$4` = top-k.

## Seed Data

`database/seed/seed.sql` inserts:
- `admin@nexai.local` / `Admin123!` (role `Admin`)
- `user@nexai.local` / `User123!` (role `User`)

Passwords are BCrypt hashes generated at seed time (never plaintext in source).
