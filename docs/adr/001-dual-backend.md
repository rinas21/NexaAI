# ADR-001: Dual Backend (ASP.NET Core API + Python FastAPI AI Service)

- **Status**: Accepted
- **Date**: 2026-11-25

## Context
The project must demonstrate both enterprise backend skills (C#/.NET) and AI engineering skills (Python AI ecosystem) without becoming a large microservices system.

## Decision
Use exactly two backend services:
- **ASP.NET Core Web API** for authentication, users, documents, permissions, conversations, audit logs, and as the public API.
- **Python FastAPI** for RAG, Semantic Kernel agents, MCP, Presidio integration, and evaluation.

The API calls the AI service over HTTP with an internal API key.

## Consequences
- Clear separation of concerns; each service uses its strongest ecosystem.
- Two services to deploy (acceptable — not microservice sprawl).
- Requires an internal service key and network isolation (Docker network).
- Frontend only talks to the ASP.NET API.
