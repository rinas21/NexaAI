# ADR-003: PostgreSQL + pgvector for Vector Storage

- **Status**: Accepted
- **Date**: 2026-11-25

## Context
Need to store document metadata, chunks, embeddings, conversations, and logs with access control in one consistent store.

## Decision
Use PostgreSQL 16 with pgvector and an HNSW cosine index on `document_chunks.embedding`, instead of a dedicated vector database.

## Consequences
- One database for relational + vector data; ACID across document deletion and chunk removal.
- Permission filtering happens in the same SQL query as vector search (prevents unauthorized retrieval).
- Sufficient for the project scale (< 1M vectors).
- For very large scale, a dedicated vector DB would be reconsidered.
