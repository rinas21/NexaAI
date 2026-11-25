# ADR-002: Local-First Inference with Ollama

- **Status**: Accepted
- **Date**: 2026-11-25

## Context
Constraint: no paid AI APIs, no API keys for inference, all data stays local, zero cost.

## Decision
Use Ollama for both chat LLM (`llama3.1:8b`) and embeddings (`nomic-embed-text`, 768 dimensions). All AI calls go through a single `OllamaClient` wrapper with retries and timeouts.

## Consequences
- No cost, strong privacy, no external dependency.
- Quality/latency below hosted frontier models; mitigated with small, grounded context and a CPU fallback model (`llama3.2:3b`).
- GPU recommended for interactive latency.
- No cloud AI experience is claimed by this implementation.
