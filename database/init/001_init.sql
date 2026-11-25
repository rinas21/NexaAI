-- NexaAI initial schema. Executed by the PostgreSQL docker entrypoint on first boot.
-- The EF Core model in src/NexaAI.Core mirrors these tables; no EF migrations are required.

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- Users & authentication
-- =============================================================================
CREATE TABLE IF NOT EXISTS users (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email          VARCHAR(255) NOT NULL UNIQUE,
    password_hash  VARCHAR(255) NOT NULL,
    full_name      VARCHAR(255),
    role           VARCHAR(50) NOT NULL DEFAULT 'User',
    is_active      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS ix_users_email ON users (email);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash  VARCHAR(255) NOT NULL UNIQUE,
    expires_at  TIMESTAMPTZ NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    revoked_at  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires_at ON refresh_tokens (expires_at);

-- =============================================================================
-- Documents & vector chunks
-- =============================================================================
CREATE TABLE IF NOT EXISTS documents (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title             VARCHAR(500) NOT NULL,
    original_filename VARCHAR(500) NOT NULL,
    content_type      VARCHAR(100) NOT NULL,
    file_size         BIGINT NOT NULL,
    storage_path      VARCHAR(1000) NOT NULL,
    owner_id          UUID REFERENCES users (id) ON DELETE SET NULL,
    access_level      VARCHAR(50) NOT NULL DEFAULT 'Private',
    status            VARCHAR(50) NOT NULL DEFAULT 'Processing',
    metadata          JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS ix_documents_owner_id ON documents (owner_id);
CREATE INDEX IF NOT EXISTS ix_documents_access_level ON documents (access_level);
CREATE INDEX IF NOT EXISTS ix_documents_status ON documents (status);
CREATE INDEX IF NOT EXISTS ix_documents_created_at ON documents (created_at);

CREATE TABLE IF NOT EXISTS document_chunks (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL REFERENCES documents (id) ON DELETE CASCADE,
    chunk_index INTEGER NOT NULL,
    content     TEXT NOT NULL,
    token_count INTEGER NOT NULL,
    embedding   VECTOR(768),
    metadata    JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    CONSTRAINT ix_document_chunks_document_chunk_index UNIQUE (document_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS ix_document_chunks_document_id ON document_chunks (document_id);
CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding
    ON document_chunks USING hnsw (embedding vector_cosine_ops);

-- =============================================================================
-- Conversations
-- =============================================================================
CREATE TABLE IF NOT EXISTS conversations (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    title      VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS ix_conversations_user_id ON conversations (user_id);
CREATE INDEX IF NOT EXISTS ix_conversations_updated_at ON conversations (updated_at);

CREATE TABLE IF NOT EXISTS messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
    role            VARCHAR(50) NOT NULL,
    content         TEXT NOT NULL,
    citations       JSONB NOT NULL DEFAULT '[]'::jsonb,
    tool_calls      JSONB NOT NULL DEFAULT '[]'::jsonb,
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS ix_messages_conversation_id ON messages (conversation_id);
CREATE INDEX IF NOT EXISTS ix_messages_created_at ON messages (created_at);

-- =============================================================================
-- Agent executions & approvals
-- =============================================================================
CREATE TABLE IF NOT EXISTS agent_executions (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID REFERENCES users (id) ON DELETE SET NULL,
    agent_name       VARCHAR(100) NOT NULL,
    input            JSONB NOT NULL DEFAULT '{}'::jsonb,
    output           JSONB,
    status           VARCHAR(50) NOT NULL DEFAULT 'Running',
    error_message    TEXT,
    execution_time_ms INTEGER,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    completed_at     TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_agent_executions_user_id ON agent_executions (user_id);
CREATE INDEX IF NOT EXISTS ix_agent_executions_status ON agent_executions (status);
CREATE INDEX IF NOT EXISTS ix_agent_executions_created_at ON agent_executions (created_at);

CREATE TABLE IF NOT EXISTS tool_approvals (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    execution_id UUID NOT NULL REFERENCES agent_executions (id) ON DELETE CASCADE,
    tool_name    VARCHAR(100) NOT NULL,
    parameters   JSONB NOT NULL DEFAULT '{}'::jsonb,
    requested_by UUID REFERENCES users (id) ON DELETE SET NULL,
    approved_by  UUID REFERENCES users (id) ON DELETE SET NULL,
    status       VARCHAR(50) NOT NULL DEFAULT 'Pending',
    reason       TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    decided_at   TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_tool_approvals_execution_id ON tool_approvals (execution_id);
CREATE INDEX IF NOT EXISTS ix_tool_approvals_status ON tool_approvals (status);

-- =============================================================================
-- Evaluation
-- =============================================================================
CREATE TABLE IF NOT EXISTS evaluation_runs (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name           VARCHAR(255) NOT NULL,
    dataset_version VARCHAR(100),
    config         JSONB NOT NULL DEFAULT '{}'::jsonb,
    status         VARCHAR(50) NOT NULL DEFAULT 'Running',
    started_at     TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    completed_at   TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_evaluation_runs_status ON evaluation_runs (status);

CREATE TABLE IF NOT EXISTS evaluation_results (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id          UUID NOT NULL REFERENCES evaluation_runs (id) ON DELETE CASCADE,
    question_id     VARCHAR(100) NOT NULL,
    question        TEXT NOT NULL,
    expected_answer TEXT,
    actual_answer   TEXT,
    retrieved_chunks JSONB NOT NULL DEFAULT '[]'::jsonb,
    metrics         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS ix_evaluation_results_run_id ON evaluation_results (run_id);

-- =============================================================================
-- Bootstrap accounts (BCrypt cost 10)
--   admin@nexai.local / Admin123!
--   user@nexai.local  / User123!
-- Change these credentials via environment-driven deployment secrets before production use.
-- =============================================================================
INSERT INTO users (id, email, password_hash, full_name, role, is_active)
VALUES
    ('11111111-1111-4111-8111-111111111111', 'admin@nexai.local',
     '$2a$10$auQTxdtpXhDXYZ8p2ETuEOwMdj0V6IMrhGB/bSY3gAb3ogiSl//vK',
     'Nexa Administrator', 'Admin', TRUE),
    ('22222222-2222-4222-8222-222222222222', 'user@nexai.local',
     '$2a$10$TgmXItdeEGwhSh1XnTzm7ORSu/5CKdLeMLAXyGKeLy/goXf7SoRXy',
     'Nexa User', 'User', TRUE)
ON CONFLICT (email) DO NOTHING;
