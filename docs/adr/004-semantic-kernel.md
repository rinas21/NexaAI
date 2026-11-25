# ADR-004: Microsoft Semantic Kernel for Agents

- **Status**: Accepted
- **Date**: 2026-11-25

## Context
Need tool calling, planning, and an agent loop that demonstrates enterprise-grade AI orchestration.

## Decision
Use Microsoft Semantic Kernel in the Python AI service with tool plugins for `document_search`, `calculator`, `datetime`, `database_query`, and `delete_document`. Sensitive tools require human approval. Execution is logged to PostgreSQL.

## Consequences
- Native function-calling conventions and plugin model; strong fit for Senior SWE–AI role.
- Dependency pinned; graceful fallback registry keeps tests runnable without SK installed.
- Tool authorization is enforced in the API before execution, not trusted to the model.
